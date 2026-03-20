using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Yobidashi.ViewModels;
using Yobidashi.Data.Models;

namespace Yobidashi.Views;

public sealed partial class SearchPopupWindow : Window
{
    public SearchPopupViewModel ViewModel { get; }

    /// <summary>定型文が選択されて挿入が要求された</summary>
    public event Action<Snippet>? SnippetInsertRequested;

    public SearchPopupWindow(SearchPopupViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();

        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();

        // ウィンドウをコンパクトに
        Title = "よびだし - 検索";

        ViewModel.SnippetSelected += OnSnippetSelected;
        ViewModel.CloseRequested += () => this.Close();
    }

    /// <summary>ポップアップ表示時の初期化</summary>
    public void ShowAndFocus()
    {
        ViewModel.Reset();
        this.Activate();
        SearchInput.Focus(FocusState.Programmatic);
    }

    private void OnSnippetSelected(Snippet snippet)
    {
        SnippetInsertRequested?.Invoke(snippet);
        this.Close();
    }

    private void SearchInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Down:
                ViewModel.SelectNextCommand.Execute(null);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Up:
                ViewModel.SelectPreviousCommand.Execute(null);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Enter:
                ViewModel.ConfirmCommand.Execute(null);
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Escape:
                this.Close();
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        ViewModel.ConfirmCommand.Execute(null);
    }
}
