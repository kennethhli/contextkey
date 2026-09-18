using System.Collections.ObjectModel;
using ContextKey.Core.Engines;
using ContextKey.Core.Models;
using ReactiveUI;

namespace ContextKey.UI.ViewModels;

public sealed class SnippetRow : ReactiveObject
{
    private string _trigger = "";
    private string _expansion = "";
    private string _description = "";

    public string Trigger
    {
        get => _trigger;
        set
        {
            this.RaiseAndSetIfChanged(ref _trigger, value);
            this.RaisePropertyChanged(nameof(Display));
        }
    }

    public string Expansion
    {
        get => _expansion;
        set => this.RaiseAndSetIfChanged(ref _expansion, value);
    }

    public string Description
    {
        get => _description;
        set
        {
            this.RaiseAndSetIfChanged(ref _description, value);
            this.RaisePropertyChanged(nameof(Display));
        }
    }

    public string Display =>
        string.IsNullOrWhiteSpace(Description) ? $"={Trigger}" : $"={Trigger}  {Description}";
}

public sealed class SettingsViewModel : ReactiveObject
{
    private readonly Action<IReadOnlyList<Snippet>> _persist;
    private SnippetRow? _selected;
    private string _status = "";

    public SettingsViewModel(IEnumerable<Snippet> snippets, Action<IReadOnlyList<Snippet>> persist)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        ArgumentNullException.ThrowIfNull(persist);
        _persist = persist;
        Items = [];
        foreach (var snippet in snippets)
        {
            Items.Add(ToRow(snippet));
        }

        Selected = Items.Count > 0 ? Items[0] : null;
    }

    public ObservableCollection<SnippetRow> Items { get; }

    public SnippetRow? Selected
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }

    public string Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public void NewSnippet()
    {
        var row = new SnippetRow { Trigger = "new", Expansion = "" };
        Items.Add(row);
        Selected = row;
        Status = "fill in the expansion, then save";
    }

    public void DeleteSelected()
    {
        if (Selected is null)
        {
            Status = "pick a snippet first";
            return;
        }

        var index = Items.IndexOf(Selected);
        Items.Remove(Selected);
        Selected = Items.Count == 0
            ? null
            : Items[Math.Clamp(index, 0, Items.Count - 1)];
        Persist("deleted");
    }

    public void Save()
    {
        foreach (var row in Items)
        {
            if (!TryNormalize(row, out var error))
            {
                Selected = row;
                Status = error;
                return;
            }
        }

        Persist("saved");
    }

    public IReadOnlyList<Snippet> ToSnippets()
    {
        var map = new Dictionary<string, Snippet>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in Items)
        {
            if (!TryNormalize(row, out _))
            {
                continue;
            }

            var trigger = StaticExpansionEngine.NormalizeTrigger(row.Trigger);
            var description = string.IsNullOrWhiteSpace(row.Description)
                ? null
                : row.Description.Trim();
            map[trigger] = new Snippet
            {
                Trigger = trigger,
                Expansion = row.Expansion.TrimEnd(),
                Description = description
            };
        }

        return map.Values
            .OrderBy(s => s.Trigger, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void Persist(string verb)
    {
        var snippets = ToSnippets();
        _persist(snippets);
        Status = $"{verb} {snippets.Count} snippet{(snippets.Count == 1 ? "" : "s")}";
    }

    private static bool TryNormalize(SnippetRow row, out string error)
    {
        error = "";
        if (!StaticExpansionEngine.TryNormalizeTrigger(row.Trigger, out var trigger))
        {
            error = "trigger needs letters, like email";
            return false;
        }

        foreach (var ch in trigger)
        {
            if (!char.IsLetterOrDigit(ch) && ch is not '_' and not '-')
            {
                error = "trigger can only use letters, numbers, _ and -";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(row.Expansion))
        {
            error = "expansion is empty";
            return false;
        }

        row.Trigger = trigger;
        return true;
    }

    private static SnippetRow ToRow(Snippet snippet) =>
        new()
        {
            Trigger = snippet.Trigger,
            Expansion = snippet.Expansion,
            Description = snippet.Description ?? ""
        };
}
