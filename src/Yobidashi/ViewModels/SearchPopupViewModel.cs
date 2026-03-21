using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yobidashi.Core.Services;
using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;

namespace Yobidashi.ViewModels;

/// <summary>
/// ポップアップウィンドウの統一表示アイテム
/// クリップボード履歴とスニペットの両方を同じリストで表示する
/// </summary>
public class PopupDisplayItem
{
    public string DisplayText { get; set; } = string.Empty;
    public string TimeText { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;
    public Brush BadgeColor { get; set; } = Brushes.Gray;
    public Visibility BadgeVisibility { get; set; } = Visibility.Collapsed;

    /// <summary>クリップボード履歴の場合はテキスト、スニペットの場合はBody</summary>
    public string FullText { get; set; } = string.Empty;

    /// <summary>スニペットの場合はSnippet参照</summary>
    public Snippet? Snippet { get; set; }

    /// <summary>クリップボード履歴アイテムかどうか</summary>
    public bool IsClipboardItem { get; set; }
}

/// <summary>
/// ポップアップの ViewModel
/// Ctrl二回連打で表示、クリップボード履歴と定型文をタブ切り替え
/// </summary>
public partial class SearchPopupViewModel : ObservableObject
{
    private readonly SnippetRepository _snippetRepository;
    private readonly ClipboardHistoryService _clipboardHistory;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PopupDisplayItem> _displayItems = new();

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _isClipboardTab = true;

    [ObservableProperty]
    private bool _isSnippetTab;

    [ObservableProperty]
    private string _itemCountText = string.Empty;

    /// <summary>アイテムが選択された（クリップボードにセット）</summary>
    public event Action<string>? ItemSelected;

    /// <summary>ポップアップを閉じる要求</summary>
    public event Action? CloseRequested;

    public SearchPopupViewModel(SnippetRepository snippetRepository, ClipboardHistoryService clipboardHistory)
    {
        _snippetRepository = snippetRepository;
        _clipboardHistory = clipboardHistory;
    }

    partial void OnSearchQueryChanged(string value)
    {
        RefreshList();
    }

    partial void OnIsClipboardTabChanged(bool value)
    {
        if (value) RefreshList();
    }

    partial void OnIsSnippetTabChanged(bool value)
    {
        if (value) RefreshList();
    }

    public void RefreshList()
    {
        DisplayItems.Clear();
        SelectedIndex = -1;

        if (IsClipboardTab)
        {
            LoadClipboardHistory();
        }
        else
        {
            LoadSnippets();
        }

        if (DisplayItems.Count > 0)
        {
            SelectedIndex = 0;
        }

        ItemCountText = $"{DisplayItems.Count}件";
    }

    private void LoadClipboardHistory()
    {
        var items = string.IsNullOrWhiteSpace(SearchQuery)
            ? _clipboardHistory.GetHistory()
            : _clipboardHistory.Search(SearchQuery);

        foreach (var entry in items.Take(100))
        {
            DisplayItems.Add(new PopupDisplayItem
            {
                DisplayText = entry.DisplayText,
                TimeText = entry.TimeText,
                FullText = entry.Text,
                IsClipboardItem = true,
                BadgeVisibility = Visibility.Collapsed
            });
        }
    }

    private void LoadSnippets()
    {
        List<Snippet> list;
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            list = _snippetRepository.GetSorted("last_used");
        }
        else
        {
            list = _snippetRepository.Search(SearchQuery);
        }

        var accentBrush = new SolidColorBrush(Color.FromRgb(0x42, 0x85, 0xF4));

        foreach (var s in list.Where(s => s.IsEnabled).Take(50))
        {
            var bodyPreview = s.Body.Split('\n')[0].Trim();
            if (bodyPreview.Length > 100) bodyPreview = bodyPreview[..100] + "...";

            DisplayItems.Add(new PopupDisplayItem
            {
                DisplayText = $"{s.Title}  {bodyPreview}",
                TimeText = s.TriggerText,
                FullText = s.Body,
                Snippet = s,
                IsClipboardItem = false,
                BadgeText = s.TriggerText,
                BadgeColor = accentBrush,
                BadgeVisibility = Visibility.Visible
            });
        }
    }

    [RelayCommand]
    private void SelectNext()
    {
        if (DisplayItems.Count == 0) return;
        SelectedIndex = Math.Min(SelectedIndex + 1, DisplayItems.Count - 1);
    }

    [RelayCommand]
    private void SelectPrevious()
    {
        if (DisplayItems.Count == 0) return;
        SelectedIndex = Math.Max(SelectedIndex - 1, 0);
    }

    [RelayCommand]
    private void Confirm()
    {
        if (SelectedIndex < 0 || SelectedIndex >= DisplayItems.Count) return;

        var item = DisplayItems[SelectedIndex];
        ItemSelected?.Invoke(item.FullText);
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
        IsClipboardTab = true;
        IsSnippetTab = false;
        RefreshList();
    }
}
