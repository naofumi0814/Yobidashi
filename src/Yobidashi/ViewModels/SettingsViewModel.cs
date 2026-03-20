using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yobidashi.Core.Services;
using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;

namespace Yobidashi.ViewModels;

/// <summary>
/// 設定画面の ViewModel
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepository;
    private readonly ExcludedAppRepository _excludedAppRepository;
    private readonly AutoStartManager _autoStartManager;
    private readonly ExportImportService _exportImportService;

    // 一般
    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private string _theme = "system";

    // 展開設定
    [ObservableProperty]
    private bool _expandOnSpace = true;

    [ObservableProperty]
    private bool _expandOnTab = true;

    // 除外アプリ
    [ObservableProperty]
    private ObservableCollection<ExcludedApp> _excludedApps = new();

    [ObservableProperty]
    private string _newExcludedProcess = string.Empty;

    [ObservableProperty]
    private string _newExcludedDisplayName = string.Empty;

    // ホットキー
    [ObservableProperty]
    private string _searchHotkey = "Ctrl+Space";

    // 情報
    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private string _version = "1.0.0";

    /// <summary>設定変更イベント</summary>
    public event Action? SettingsChanged;

    public SettingsViewModel(
        SettingsRepository settingsRepository,
        ExcludedAppRepository excludedAppRepository,
        AutoStartManager autoStartManager,
        ExportImportService exportImportService)
    {
        _settingsRepository = settingsRepository;
        _excludedAppRepository = excludedAppRepository;
        _autoStartManager = autoStartManager;
        _exportImportService = exportImportService;

        LoadSettings();
    }

    private void LoadSettings()
    {
        AutoStart = _autoStartManager.IsEnabled;
        Theme = _settingsRepository.Get("theme") ?? "system";

        var triggerKeys = _settingsRepository.Get("trigger_key") ?? "Space,Tab";
        ExpandOnSpace = triggerKeys.Contains("Space", StringComparison.OrdinalIgnoreCase);
        ExpandOnTab = triggerKeys.Contains("Tab", StringComparison.OrdinalIgnoreCase);

        SearchHotkey = _settingsRepository.Get("hotkey_search") ?? "Ctrl+Space";
        DatabasePath = Data.DatabaseManager.DatabasePath;

        LoadExcludedApps();
    }

    private void LoadExcludedApps()
    {
        var apps = _excludedAppRepository.GetAll();
        ExcludedApps.Clear();
        foreach (var app in apps)
        {
            ExcludedApps.Add(app);
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _autoStartManager.SetEnabled(AutoStart);
        _settingsRepository.Set("theme", Theme);

        var keys = new List<string>();
        if (ExpandOnSpace) keys.Add("Space");
        if (ExpandOnTab) keys.Add("Tab");
        _settingsRepository.Set("trigger_key", string.Join(",", keys));

        _settingsRepository.Set("hotkey_search", SearchHotkey);

        SettingsChanged?.Invoke();
    }

    [RelayCommand]
    private void AddExcludedApp()
    {
        if (string.IsNullOrWhiteSpace(NewExcludedProcess)) return;
        _excludedAppRepository.Insert(NewExcludedProcess.Trim(), NewExcludedDisplayName.Trim());
        NewExcludedProcess = string.Empty;
        NewExcludedDisplayName = string.Empty;
        LoadExcludedApps();
        SettingsChanged?.Invoke();
    }

    [RelayCommand]
    private void RemoveExcludedApp(ExcludedApp? app)
    {
        if (app == null) return;
        _excludedAppRepository.Delete(app.Id);
        LoadExcludedApps();
        SettingsChanged?.Invoke();
    }

    [RelayCommand]
    private async Task ExportData()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON ファイル (*.json)|*.json",
            FileName = $"yobidashi_export_{DateTime.Now:yyyyMMdd}",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _exportImportService.ExportAsync(dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task ImportData()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON ファイル (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            await _exportImportService.ImportAsync(dialog.FileName);
        }
    }

    [RelayCommand]
    private async Task Backup()
    {
        await _exportImportService.BackupAsync();
    }
}
