using System.Reflection;
using ContextKey.Core.Interfaces;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ContextKey.Infrastructure.Ai;

public sealed class OnnxMiniLmEmbedder : ITextEmbedder, IDisposable
{
    public const int HiddenSize = 384;
    public const string ModelFileName = "all-MiniLM-L6-v2-quantized.onnx";

    private const string DefaultModelUrl =
        "https://huggingface.co/Xenova/all-MiniLM-L6-v2/resolve/main/onnx/model_quantized.onnx";

    private readonly InferenceSession _session;
    private readonly MiniLmWordPiece _tokenizer;
    private readonly object _gate = new();
    private readonly string _inputIdsName;
    private readonly string _maskName;
    private readonly string? _tokenTypeName;
    private readonly string _outputName;
    private bool _disposed;

    private OnnxMiniLmEmbedder(InferenceSession session, MiniLmWordPiece tokenizer)
    {
        _session = session;
        _tokenizer = tokenizer;

        var inputs = session.InputMetadata.Keys.ToArray();
        _inputIdsName = FindInput(inputs, "input_ids") ?? inputs[0];
        _maskName = FindInput(inputs, "attention_mask") ?? (inputs.Length > 1 ? inputs[1] : inputs[0]);
        _tokenTypeName = FindInput(inputs, "token_type_ids");
        _outputName = session.OutputMetadata.Keys.First();
    }

    public int Dimensions => HiddenSize;
    public bool IsAvailable => !_disposed;

    public static OnnxMiniLmEmbedder? TryCreate()
    {
        try
        {
            var vocab = LoadVocab();
            if (vocab.Count == 0)
            {
                return null;
            }

            var modelPath = EnsureModel();
            if (modelPath is null)
            {
                return null;
            }

            var options = new SessionOptions
            {
                InterOpNumThreads = 1,
                IntraOpNumThreads = 1
            };

            var session = new InferenceSession(modelPath, options);
            return new OnnxMiniLmEmbedder(session, MiniLmWordPiece.FromLines(vocab));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"onnx embedder skipped: {ex.Message}");
            return null;
        }
    }

    public float[] Embed(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var (ids, mask) = _tokenizer.Encode(text ?? string.Empty);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputIdsName, Tensor(ids)),
            NamedOnnxValue.CreateFromTensor(_maskName, Tensor(mask))
        };

        if (_tokenTypeName is not null)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor(_tokenTypeName, Tensor(new long[MiniLmWordPiece.MaxTokens])));
        }

        lock (_gate)
        {
            using var results = _session.Run(inputs);
            var output = results.FirstOrDefault(r => r.Name == _outputName) ?? results.First();
            return Pool(output.AsTensor<float>(), mask);
        }
    }

    public void Warmup()
    {
        try
        {
            _ = Embed("ok");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"onnx warmup failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _session.Dispose();
    }

    public static string ModelDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "ContextKey", "models");
    }

    private static DenseTensor<long> Tensor(long[] values)
    {
        var tensor = new DenseTensor<long>(new[] { 1, values.Length });
        for (var i = 0; i < values.Length; i++)
        {
            tensor[0, i] = values[i];
        }

        return tensor;
    }

    private static float[] Pool(Tensor<float> hidden, long[] mask)
    {
        // [1, seq, 384] last_hidden_state, or [1, 384] already pooled
        var dims = hidden.Dimensions;
        if (dims.Length == 2)
        {
            var already = new float[HiddenSize];
            var n = Math.Min(HiddenSize, dims[1]);
            for (var i = 0; i < n; i++)
            {
                already[i] = hidden[0, i];
            }

            Normalize(already);
            return already;
        }

        var seq = dims[1];
        var width = dims[2];
        var pooled = new float[width];
        var count = 0f;
        for (var t = 0; t < seq && t < mask.Length; t++)
        {
            if (mask[t] == 0)
            {
                continue;
            }

            count++;
            for (var h = 0; h < width; h++)
            {
                pooled[h] += hidden[0, t, h];
            }
        }

        if (count > 0)
        {
            for (var h = 0; h < width; h++)
            {
                pooled[h] /= count;
            }
        }

        Normalize(pooled);
        return pooled;
    }

    private static void Normalize(float[] vector)
    {
        double sum = 0;
        foreach (var v in vector)
        {
            sum += v * v;
        }

        var norm = (float)Math.Sqrt(sum);
        if (norm < 1e-8)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }

    private static string? FindInput(IReadOnlyList<string> names, string needle) =>
        names.FirstOrDefault(n => n.Equals(needle, StringComparison.OrdinalIgnoreCase));

    private static List<string> LoadVocab()
    {
        var assembly = typeof(OnnxMiniLmEmbedder).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("vocab.txt", StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return [];
        }

        using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return [];
        }

        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static string? EnsureModel()
    {
        var dir = ModelDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, ModelFileName);
        if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
        {
            return path;
        }

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ContextKey/1.0");
        using var response = client.GetAsync(DefaultModelUrl).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        var tmp = path + ".tmp";
        using (var input = response.Content.ReadAsStream())
        using (var output = File.Create(tmp))
        {
            input.CopyTo(output);
        }

        File.Move(tmp, path, overwrite: true);
        return path;
    }
}
