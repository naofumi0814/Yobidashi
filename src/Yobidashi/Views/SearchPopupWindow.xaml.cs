using System.Windows;
using System.Windows.Input;
using Yobidashi.ViewModels;

namespace Yobidashi.Views;

public partial class SearchPopupWindow : Window
{
    public SearchPopupViewModel ViewModel { get; }

    /// <summary>テキストが選択された（クリップボードにセット済み）</summary>
    public event Action<string>? TextSelected;

    public SearchPopupWindow(SearchPopupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        ViewModel.ItemSelected += OnItemSelected;
        ViewModel.CloseRequested += () => this.Hide();
    }

    public void ShowAndFocus()
    {
        ViewModel.Reset();
        this.Show();
        this.Activate();
        SearchInput.Focus();
        SearchInput.SelectAll();
    }

    private void OnItemSelected(string text)
    {
        // クリップボードにセットして閉じる
        try
        {
            Clipboard.SetText(text);
        }
        catch { }

        this.Hide();
        TextSelected?.Invoke(text);
    }

    private void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                ViewModel.SelectNextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                ViewModel.SelectPreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter:
                ViewModel.ConfirmCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                this.Hide();
                e.Handled = true;
                break;
            case Key.Tab:
                // Tab でタブ切り替え
                if (ViewModel.IsClipboardTab)
                {
                    ViewModel.IsClipboardTab = false;
                    ViewModel.IsSnippetTab = true;
                }
                else
                {
                    ViewModel.IsClipboardTab = true;
                    ViewModel.IsSnippetTab = false;
                }
                e.Handled = true;
                break;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            this.Hide();
            e.Handled = true;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        this.Hide();
    }

    private void ResultsList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel.ConfirmCommand.Execute(null);
    }

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        SearchInput?.Focus();
    }
}
