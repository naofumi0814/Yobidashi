using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yobidashi.ViewModels;

namespace Yobidashi.Views;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();

        // Mica背景を設定
        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();

        // タイトルバーカスタマイズ
        ExtendsContentIntoTitleBar = false;
        Title = "Yobidashi - よびだし";
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        // ExpansionServiceのIsPausedと連動（App.xaml.csで接続）
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // 設定ウィンドウを開く
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

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "削除の確認",
            Content = "この定型文を削除しますか？この操作は元に戻せません。",
            PrimaryButtonText = "削除",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.DeleteCommand.Execute(null);
        }
    }

    private async void AddCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox { PlaceholderText = "カテゴリ名を入力" };
        var dialog = new ContentDialog
        {
            Title = "カテゴリを追加",
            Content = input,
            PrimaryButtonText = "追加",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
        {
            ViewModel.AddCategoryCommand.Execute(input.Text);
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
