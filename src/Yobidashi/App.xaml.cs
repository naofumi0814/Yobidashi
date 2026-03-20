using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Yobidashi.Core.Expansion;
using Yobidashi.Core.Hooks;
using Yobidashi.Core.Services;
using Yobidashi.Data;
using Yobidashi.Data.Repositories;
using Yobidashi.ViewModels;
using Yobidashi.Views;

namespace Yobidashi;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private SearchPopupWindow? _searchPopupWindow;
    private ExpansionService? _expansionService;
    private HotkeyManager? _hotkeyManager;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public new static App Current => (App)Application.Current;
    public IServiceProvider Services => _serviceProvider!;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // DB初期化
        var db = _serviceProvider.GetRequiredService<DatabaseManager>();
        db.Initialize();

        // メインウィンドウ
        _mainWindow = new MainWindow(_serviceProvider.GetRequiredService<MainViewModel>());

        bool startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
        {
            _mainWindow.Show();
        }

        SetupTrayIcon();
        StartExpansionService();
        SetupHotkeys();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DatabaseManager>();
        services.AddSingleton<SnippetRepository>();
        services.AddSingleton<CategoryRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<ExcludedAppRepository>();

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

        services.AddTransient<MainViewModel>();
        services.AddTransient<SearchPopupViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Yobidashi - よびだし",
            Visible = true
        };

        // 埋め込みアイコン（なければデフォルト）
        try
        {
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (System.IO.File.Exists(iconPath))
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);
            else
                _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("管理画面を開く", null, (s, e) => ShowMainWindow());
        menu.Items.Add("検索 (Ctrl+Space)", null, (s, e) => ShowSearchPopup());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var pauseItem = new System.Windows.Forms.ToolStripMenuItem("一時停止");
        pauseItem.Click += (s, e) =>
        {
            if (_expansionService != null)
            {
                _expansionService.IsPaused = !_expansionService.IsPaused;
                pauseItem.Text = _expansionService.IsPaused ? "再開" : "一時停止";
                _trayIcon.Text = _expansionService.IsPaused
                    ? "Yobidashi - 一時停止中"
                    : "Yobidashi - よびだし";
            }
        };
        menu.Items.Add(pauseItem);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("終了", null, (s, e) => ExitApplication());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) => ShowMainWindow();
    }

    private void StartExpansionService()
    {
        _expansionService = _serviceProvider!.GetRequiredService<ExpansionService>();
        _expansionService.SnippetExpanded += (snippet) =>
        {
            _mainWindow?.Dispatcher.Invoke(() =>
            {
                _mainWindow.ViewModel.StatusText = $"展開完了: {snippet.Title}";
                _mainWindow.ViewModel.LoadData();
            });
        };
        _expansionService.StatusMessage += (msg) =>
        {
            _mainWindow?.Dispatcher.Invoke(() =>
            {
                _mainWindow.ViewModel.StatusText = msg;
            });
        };
        _expansionService.Start();
    }

    private void SetupHotkeys()
    {
        _hotkeyManager = _serviceProvider!.GetRequiredService<HotkeyManager>();

        if (_mainWindow != null)
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(_mainWindow);
            helper.EnsureHandle();
            _hotkeyManager.Initialize(helper.Handle);

            _hotkeyManager.Register(
                Interop.NativeMethods.MOD_CONTROL,
                Interop.NativeMethods.VK_SPACE,
                ShowSearchPopup);

            // WM_HOTKEY メッセージを処理
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
            source?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == 0x0312) // WM_HOTKEY
                {
                    _hotkeyManager.HandleHotkeyMessage(wParam.ToInt32());
                    handled = true;
                }
                return IntPtr.Zero;
            });
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow(_serviceProvider!.GetRequiredService<MainViewModel>());
        }
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow == null)
        {
            var vm = _serviceProvider!.GetRequiredService<SettingsViewModel>();
            vm.SettingsChanged += () => _expansionService?.RefreshExcludedApps();
            _settingsWindow = new SettingsWindow(vm);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        }
        _settingsWindow.Show();
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
                    await _expansionService.ExpandDirectAsync(snippet);
            };
            _searchPopupWindow.Closed += (s, e) => _searchPopupWindow = null;
        }
        _searchPopupWindow.ShowAndFocus();
    }

    private void ExitApplication()
    {
        _expansionService?.Dispose();
        _hotkeyManager?.Dispose();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        (_serviceProvider as IDisposable)?.Dispose();
        Shutdown();
    }
}
