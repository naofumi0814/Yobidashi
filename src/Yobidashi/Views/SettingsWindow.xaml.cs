using System.Windows;
using Yobidashi.Data.Models;
using Yobidashi.ViewModels;

namespace Yobidashi.Views;

public partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveSettingsCommand.Execute(null);
        MessageBox.Show("設定を保存しました。", "Yobidashi", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void AddExcludedApp_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddExcludedAppCommand.Execute(null);
    }

    private void RemoveExcludedApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ExcludedApp app)
        {
            ViewModel.RemoveExcludedAppCommand.Execute(app);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ExportDataCommand.Execute(null);
        MessageBox.Show("エクスポートが完了しました。", "Yobidashi", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ImportDataCommand.Execute(null);
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BackupCommand.Execute(null);
        MessageBox.Show("バックアップが完了しました。", "Yobidashi", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
