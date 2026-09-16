using System.Globalization;
using System.Text;

namespace ContextKey.Infrastructure.Ai;

internal sealed class MiniLmWordPiece
{
    public const int MaxTokens = 128;

    private readonly Dictionary<string, int> _vocab;
    private readonly int _unk;
    private readonly int _cls;
    private readonly int _sep;

    public MiniLmWordPiece(IEnumerable<string> tokens)
    {
        _vocab = new Dictionary<string, int>(StringComparer.Ordinal);
        var i = 0;
        foreach (var token in tokens)
        {
            if (token.Length > 0)
            {
                _vocab[token] = i;
            }

            i++;
        }

        _unk = Id("[UNK]");
        _cls = Id("[CLS]");
        _sep = Id("[SEP]");
    }

    public static MiniLmWordPiece FromLines(IEnumerable<string> lines) =>
        new(lines.Select(l => l.TrimEnd('\r')));

    public (long[] Ids, long[] Mask) Encode(string text)
    {
        var pieces = new List<int>(MaxTokens) { _cls };
        foreach (var word in BasicTokens(text))
        {
            foreach (var id in WordPieces(word))
            {
                if (pieces.Count >= MaxTokens - 1)
                {
                    break;
                }

                pieces.Add(id);
            }

            if (pieces.Count >= MaxTokens - 1)
            {
                break;
            }
        }

        pieces.Add(_sep);

        var ids = new long[MaxTokens];
        var mask = new long[MaxTokens];
        for (var i = 0; i < pieces.Count && i < MaxTokens; i++)
        {
            ids[i] = pieces[i];
            mask[i] = 1;
        }

        return (ids, mask);
    }

    private int Id(string token) =>
        _vocab.TryGetValue(token, out var id) ? id : _unk;

    private IEnumerable<int> WordPieces(string token)
    {
        if (_vocab.ContainsKey(token))
        {
            yield return _vocab[token];
            yield break;
        }

        var start = 0;
        var unknown = false;
        while (start < token.Length)
        {
            var end = token.Length;
            var found = -1;
            while (start < end)
            {
                var piece = start == 0 ? token[start..end] : "##" + token[start..end];
                if (_vocab.TryGetValue(piece, out var id))
                {
                    found = id;
                    break;
                }

                end--;
            }

            if (found < 0)
            {
                unknown = true;
                break;
            }

            yield return found;
            start = end;
        }

        if (unknown)
        {
            yield return _unk;
        }
    }

    private static IEnumerable<string> BasicTokens(string text)
    {
        var form = text.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder();

        foreach (var raw in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var ch = char.ToLowerInvariant(raw);
            if (char.IsWhiteSpace(ch))
            {
                if (buffer.Length > 0)
                {
                    yield return buffer.ToString();
                    buffer.Clear();
                }

                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(ch);
                continue;
            }

            if (buffer.Length > 0)
            {
                yield return buffer.ToString();
                buffer.Clear();
            }

            yield return ch.ToString();
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }
}
