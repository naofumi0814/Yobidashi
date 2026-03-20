using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Yobidashi.Core.Expansion;
using Yobidashi.Core.Hooks;
using Yobidashi.Core.Services;
using Yobidashi.Data;
using Yobidashi.Data.Repositories;
using Yobidashi.ViewModels;
using Yobidashi.Views;

namespace Yobidashi;

/// <summary>
/// Yobidashi アプリケーション エントリポイント
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private SearchPopupWindow? _searchPopupWindow;
    private ExpansionService? _expansionService;
    private HotkeyManager? _hotkeyManager;
    private H.NotifyIcon.TaskbarIcon? _trayIcon;

    public new static App Current => (App)Application.Current;

    public IServiceProvider Services => _serviceProvider!;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // DIコンテナの構築
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // データベース初期化
        var db = _serviceProvider.GetRequiredService<DatabaseManager>();
        db.Initialize();

        // メインウィンドウ作成
        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

        // 起動パラメータで最小化起動するか判定
        bool startMinimized = Environment.GetCommandLineArgs().Contains("--minimized");

        if (!startMinimized)
        {
            _mainWindow.Activate();
        }

        // システムトレイ設定
        SetupTrayIcon();

        // 展開サービス開始
        StartExpansionService();

        // グローバルホットキー設定
        SetupHotkeys();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // データ
        services.AddSingleton<DatabaseManager>();
        services.AddSingleton<SnippetRepository>();
        services.AddSingleton<CategoryRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<ExcludedAppRepository>();

        // コアサービス
        services.AddSingleton<GlobalKeyboardHook>();
        services.AddSingleton<ImeStateDetector>();
        services.AddSingleton<ITextNormalizer, TextNormalizer>();
        services.AddSingleton<TriggerDetector>();
        services.AddSingleton<VariableProcessor>();
        services.AddSingleton<TextExpander>();
        services.AddSingleton<ExpansionService>();
        services.AddSingleton<HotkeyManager>();
        services.AddSingleton<AutoStartManager>();
        services.AddSingleton<ExportImportService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SearchPopupViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    private void SetupTrayIcon()
    {
        // H.NotifyIcon を使用したシステムトレイアイコン
        // 注: 実際の実装では XAML リソースまたはアイコンファイルを使用
        _trayIcon = new H.NotifyIcon.TaskbarIcon();
        _trayIcon.ToolTipText = "Yobidashi - よびだし";

        // コンテキストメニュー
        var contextMenu = new Microsoft.UI.Xaml.Controls.MenuFlyout();

        var showItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "管理画面を開く" };
        showItem.Click += (s, e) => ShowMainWindow();
        contextMenu.Items.Add(showItem);

        var searchItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "検索 (Ctrl+Space)" };
        searchItem.Click += (s, e) => ShowSearchPopup();
        contextMenu.Items.Add(searchItem);

        contextMenu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());

        var pauseItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "一時停止" };
        pauseItem.Click += (s, e) =>
        {
            if (_expansionService != null)
            {
                _expansionService.IsPaused = !_expansionService.IsPaused;
                pauseItem.Text = _expansionService.IsPaused ? "再開" : "一時停止";
                _trayIcon.ToolTipText = _expansionService.IsPaused
                    ? "Yobidashi - 一時停止中"
                    : "Yobidashi - よびだし";
            }
        };
        contextMenu.Items.Add(pauseItem);

        contextMenu.Items.Add(new Microsoft.UI.Xaml.Controls.MenuFlyoutSeparator());

        var exitItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "終了" };
        exitItem.Click += (s, e) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        _trayIcon.ContextMenuMode = H.NotifyIcon.ContextMenuMode.SecondWindow;
        _trayIcon.ContextFlyout = contextMenu;

        // ダブルクリックでメインウィンドウ表示
        _trayIcon.TrayIconLeftMouseDoubleClick += (s, e) => ShowMainWindow();

        _trayIcon.ForceCreate();
    }

    private void StartExpansionService()
    {
        _expansionService = _serviceProvider!.GetRequiredService<ExpansionService>();
        _expansionService.SnippetExpanded += (snippet) =>
        {
            // UIスレッドで通知更新
            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                _mainWindow.ViewModel.StatusText = $"展開完了: {snippet.Title}";
                _mainWindow.ViewModel.LoadData();
            });
        };
        _expansionService.StatusMessage += (msg) =>
        {
            _mainWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                _mainWindow.ViewModel.StatusText = msg;
            });
        };
        _expansionService.Start();
    }

    private void SetupHotkeys()
    {
        _hotkeyManager = _serviceProvider!.GetRequiredService<HotkeyManager>();

        // メインウィンドウのハンドルでホットキー登録
        if (_mainWindow != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_mainWindow);
            _hotkeyManager.Initialize(hwnd);

            // Ctrl+Space で検索ポップアップ
            _hotkeyManager.Register(
                Interop.NativeMethods.MOD_CONTROL,
                Interop.NativeMethods.VK_SPACE,
                ShowSearchPopup);
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
        }
        _mainWindow.Activate();
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow == null)
        {
            var vm = _serviceProvider!.GetRequiredService<SettingsViewModel>();
            vm.SettingsChanged += () =>
            {
                _expansionService?.RefreshExcludedApps();
            };
            _settingsWindow = new SettingsWindow(vm);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        }
        _settingsWindow.Activate();
    }

    public void ShowSearchPopup()
    {
        if (_searchPopupWindow == null)
        {
            var vm = _serviceProvider!.GetRequiredService<SearchPopupViewModel>();
            _searchPopupWindow = new SearchPopupWindow(vm);
            _searchPopupWindow.SnippetInsertRequested += async (snippet) =>
            {
                if (_expansionService != null)
                {
                    await _expansionService.ExpandDirectAsync(snippet);
                }
            };
            _searchPopupWindow.Closed += (s, e) => _searchPopupWindow = null;
        }
        _searchPopupWindow.ShowAndFocus();
    }

    private void ExitApplication()
    {
        _expansionService?.Dispose();
        _hotkeyManager?.Dispose();
        _trayIcon?.Dispose();
        (_serviceProvider as IDisposable)?.Dispose();
        Environment.Exit(0);
    }
}
