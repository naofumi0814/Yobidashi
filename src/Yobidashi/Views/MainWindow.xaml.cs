using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Yobidashi.ViewModels;

namespace Yobidashi.Views;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // 閉じる代わりに非表示（トレイに最小化）
        e.Cancel = true;
        this.Hide();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        // ViewModel.IsPaused は TwoWay binding で自動更新
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        App.Current.ShowSettingsWindow();
    }

    private void NewSnippetButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NewSnippetCommand.Execute(null);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveCommand.Execute(null);
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "この定型文を削除しますか？\nこの操作は元に戻せません。",
            "削除の確認",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            ViewModel.DeleteCommand.Execute(null);
        }
    }

    private void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("カテゴリを追加", "カテゴリ名を入力してください:");
        dialog.Owner = this;
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            ViewModel.AddCategoryCommand.Execute(dialog.InputText);
        }
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
        {
            ViewModel.SortBy = item.Tag?.ToString() ?? "updated";
        }
    }
}
