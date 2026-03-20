using System.Windows;
using System.Windows.Input;
using Yobidashi.Data.Models;
using Yobidashi.ViewModels;

namespace Yobidashi.Views;

public partial class SearchPopupWindow : Window
{
    public SearchPopupViewModel ViewModel { get; }

    public event Action<Snippet>? SnippetInsertRequested;

    public SearchPopupWindow(SearchPopupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        ViewModel.SnippetSelected += OnSnippetSelected;
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

    private void OnSnippetSelected(Snippet snippet)
    {
        this.Hide();
        SnippetInsertRequested?.Invoke(snippet);
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
}
