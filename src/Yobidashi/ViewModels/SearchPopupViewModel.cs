using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;

namespace Yobidashi.ViewModels;

/// <summary>
/// 検索ポップアップの ViewModel
/// </summary>
public partial class SearchPopupViewModel : ObservableObject
{
    private readonly SnippetRepository _snippetRepository;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Snippet> _results = new();

    [ObservableProperty]
    private Snippet? _selectedResult;

    [ObservableProperty]
    private int _selectedIndex = -1;

    /// <summary>定型文が選択された（挿入実行）</summary>
    public event Action<Snippet>? SnippetSelected;

    /// <summary>ポップアップを閉じる要求</summary>
    public event Action? CloseRequested;

    public SearchPopupViewModel(SnippetRepository snippetRepository)
    {
        _snippetRepository = snippetRepository;
    }

    partial void OnSearchQueryChanged(string value)
    {
        Search(value);
    }

    private void Search(string query)
    {
        Results.Clear();
        SelectedIndex = -1;

        List<Snippet> list;
        if (string.IsNullOrWhiteSpace(query))
        {
            // 空クエリ: 最近使用した順で上位を表示
            list = _snippetRepository.GetSorted("last_used");
        }
        else
        {
            list = _snippetRepository.Search(query);
        }

        foreach (var s in list.Where(s => s.IsEnabled).Take(20))
        {
            Results.Add(s);
        }

        if (Results.Count > 0)
        {
            SelectedIndex = 0;
            SelectedResult = Results[0];
        }
    }

    [RelayCommand]
    private void SelectNext()
    {
        if (Results.Count == 0) return;
        SelectedIndex = Math.Min(SelectedIndex + 1, Results.Count - 1);
        SelectedResult = Results[SelectedIndex];
    }

    [RelayCommand]
    private void SelectPrevious()
    {
        if (Results.Count == 0) return;
        SelectedIndex = Math.Max(SelectedIndex - 1, 0);
        SelectedResult = Results[SelectedIndex];
    }

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedResult != null)
        {
            SnippetSelected?.Invoke(SelectedResult);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }

    /// <summary>ポップアップ表示時の初期化</summary>
    public void Reset()
    {
        SearchQuery = string.Empty;
        Results.Clear();
        SelectedIndex = -1;
        // デフォルトで最近使ったものを表示
        Search(string.Empty);
    }
}
