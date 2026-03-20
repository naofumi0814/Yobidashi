using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;
using Yobidashi.Core.Services;
using Yobidashi.Core.Expansion;

namespace Yobidashi.ViewModels;

/// <summary>
/// メインウィンドウの ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SnippetRepository _snippetRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly VariableProcessor _variableProcessor;

    [ObservableProperty]
    private ObservableCollection<Snippet> _snippets = new();

    [ObservableProperty]
    private ObservableCollection<string> _categories = new();

    [ObservableProperty]
    private Snippet? _selectedSnippet;

    [ObservableProperty]
    private string _selectedCategory = "すべて";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusText = "監視中";

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _snippetCount;

    // 編集フォーム
    [ObservableProperty]
    private string _editTitle = string.Empty;

    [ObservableProperty]
    private string _editTrigger = string.Empty;

    [ObservableProperty]
    private string _editBody = string.Empty;

    [ObservableProperty]
    private string _editCategory = string.Empty;

    [ObservableProperty]
    private string _editTags = string.Empty;

    [ObservableProperty]
    private string _editMemo = string.Empty;

    [ObservableProperty]
    private bool _editIsEnabled = true;

    [ObservableProperty]
    private string _previewText = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _sortBy = "updated";

    public MainViewModel(
        SnippetRepository snippetRepository,
        CategoryRepository categoryRepository,
        VariableProcessor variableProcessor)
    {
        _snippetRepository = snippetRepository;
        _categoryRepository = categoryRepository;
        _variableProcessor = variableProcessor;

        LoadData();
    }

    public void LoadData()
    {
        LoadCategories();
        LoadSnippets();
    }

    private void LoadCategories()
    {
        var cats = _categoryRepository.GetAll();
        Categories.Clear();
        Categories.Add("すべて");
        foreach (var cat in cats)
        {
            Categories.Add(cat.Name);
        }
    }

    private void LoadSnippets()
    {
        List<Snippet> list;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            list = _snippetRepository.Search(SearchQuery);
        }
        else if (SelectedCategory != "すべて" && !string.IsNullOrEmpty(SelectedCategory))
        {
            list = _snippetRepository.GetByCategory(SelectedCategory);
        }
        else
        {
            list = _snippetRepository.GetSorted(SortBy);
        }

        Snippets.Clear();
        foreach (var s in list)
        {
            Snippets.Add(s);
        }
        SnippetCount = Snippets.Count;
    }

    partial void OnSelectedSnippetChanged(Snippet? value)
    {
        if (value != null)
        {
            EditTitle = value.Title;
            EditTrigger = value.TriggerText;
            EditBody = value.Body;
            EditCategory = value.Category;
            EditTags = string.Join(", ", value.GetTagList());
            EditMemo = value.Memo;
            EditIsEnabled = value.IsEnabled;
            IsEditing = true;
            UpdatePreview();
        }
        else
        {
            ClearEditForm();
        }
    }

    partial void OnEditBodyChanged(string value)
    {
        UpdatePreview();
    }

    partial void OnSearchQueryChanged(string value)
    {
        LoadSnippets();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        LoadSnippets();
    }

    partial void OnSortByChanged(string value)
    {
        LoadSnippets();
    }

    private void UpdatePreview()
    {
        if (!string.IsNullOrEmpty(EditBody))
        {
            PreviewText = _variableProcessor.Preview(EditBody);
        }
        else
        {
            PreviewText = string.Empty;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditTrigger)) return;

        var tagList = EditTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        if (SelectedSnippet != null && SelectedSnippet.Id > 0)
        {
            // 更新
            SelectedSnippet.Title = EditTitle;
            SelectedSnippet.TriggerText = EditTrigger;
            SelectedSnippet.Body = EditBody;
            SelectedSnippet.Category = EditCategory;
            SelectedSnippet.SetTagList(tagList);
            SelectedSnippet.Memo = EditMemo;
            SelectedSnippet.IsEnabled = EditIsEnabled;
            _snippetRepository.Update(SelectedSnippet);
        }
        else
        {
            // 新規作成
            var snippet = new Snippet
            {
                Title = EditTitle,
                TriggerText = EditTrigger,
                Body = EditBody,
                Category = EditCategory,
                Memo = EditMemo,
                IsEnabled = EditIsEnabled,
            };
            snippet.SetTagList(tagList);
            snippet.Id = _snippetRepository.Insert(snippet);
        }

        LoadSnippets();
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedSnippet == null || SelectedSnippet.Id == 0) return;
        _snippetRepository.Delete(SelectedSnippet.Id);
        ClearEditForm();
        LoadSnippets();
    }

    [RelayCommand]
    private void NewSnippet()
    {
        SelectedSnippet = null;
        ClearEditForm();
        EditCategory = SelectedCategory == "すべて" ? "" : SelectedCategory;
        IsEditing = true;
    }

    [RelayCommand]
    private void AddCategory(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        _categoryRepository.Insert(name);
        LoadCategories();
    }

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    private void ClearEditForm()
    {
        EditTitle = string.Empty;
        EditTrigger = string.Empty;
        EditBody = string.Empty;
        EditCategory = string.Empty;
        EditTags = string.Empty;
        EditMemo = string.Empty;
        EditIsEnabled = true;
        PreviewText = string.Empty;
        IsEditing = false;
    }
}
