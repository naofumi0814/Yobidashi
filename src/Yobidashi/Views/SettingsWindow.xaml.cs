using Microsoft.UI.Xaml;
using Yobidashi.Data.Models;
using Yobidashi.ViewModels;

namespace Yobidashi.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();

        this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        Title = "設定 - Yobidashi";
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveSettingsCommand.Execute(null);
        this.Close();
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
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ImportDataCommand.Execute(null);
    }

    private void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.BackupCommand.Execute(null);
    }
}
