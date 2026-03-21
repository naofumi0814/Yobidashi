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
        this.Focus();
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

    private void SwitchTab()
    {
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
    }

    private void ScrollSelectedIntoView()
    {
        if (ViewModel.SelectedIndex >= 0 && ViewModel.SelectedIndex < ResultsList.Items.Count)
        {
            ResultsList.ScrollIntoView(ResultsList.Items[ViewModel.SelectedIndex]);
        }
    }

    /// <summary>
    /// PreviewKeyDownでウィンドウレベルでキー入力を最優先処理
    /// 子コントロールにイベントが渡る前にハンドルする
    /// </summary>
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                ViewModel.SelectNextCommand.Execute(null);
                ScrollSelectedIntoView();
                e.Handled = true;
                break;
            case Key.Up:
                ViewModel.SelectPreviousCommand.Execute(null);
                ScrollSelectedIntoView();
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Right:
                SwitchTab();
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
                SwitchTab();
                e.Handled = true;
                break;
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
}
