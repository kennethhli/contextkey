using System.Collections.ObjectModel;
using ContextKey.Core.Interfaces;
using ContextKey.Core.Models;
using ReactiveUI;

namespace ContextKey.UI.ViewModels;

public sealed class FloatingOverlayViewModel : ReactiveObject
{
    private readonly ISearchEngine _search;
    private readonly IReadOnlyList<Snippet> _snippets;
    private IReadOnlyList<ScrapedWindow> _windows;
    private string _query;
    private SearchResult? _selected;

    public FloatingOverlayViewModel(
        ISearchEngine search,
        IReadOnlyList<Snippet> snippets,
        IReadOnlyList<ScrapedWindow> windows,
        string query)
    {
        _search = search;
        _snippets = snippets;
        _windows = windows;
        _query = query;
        Results = [];
        Refresh();
    }

    public ObservableCollection<SearchResult> Results { get; }

    public string Query
    {
        get => _query;
        set
        {
            this.RaiseAndSetIfChanged(ref _query, value);
            Refresh();
        }
    }

    public SearchResult? Selected
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }

    public void ReplaceWindows(IReadOnlyList<ScrapedWindow> windows)
    {
        _windows = windows;
        Refresh();
    }

    public void MoveSelection(int delta)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var index = Selected is null ? 0 : Results.IndexOf(Selected);
        if (index < 0)
        {
            index = 0;
        }

        index = Math.Clamp(index + delta, 0, Results.Count - 1);
        Selected = Results[index];
    }

    public string? Confirm() => Selected?.Value;

    private void Refresh()
    {
        Results.Clear();
        foreach (var hit in _search.Search(_query, _snippets, _windows))
        {
            Results.Add(hit);
        }

        Selected = Results.Count > 0 ? Results[0] : null;
    }
}
