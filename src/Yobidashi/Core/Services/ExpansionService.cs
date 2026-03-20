using Yobidashi.Core.Expansion;
using Yobidashi.Core.Hooks;
using Yobidashi.Data.Models;
using Yobidashi.Data.Repositories;
using Yobidashi.Interop;

namespace Yobidashi.Core.Services;

/// <summary>
/// 展開サービス
/// キーボードフック → IME判定 → トリガー検知 → テキスト展開 を統合管理する
/// </summary>
public class ExpansionService : IDisposable
{
    private readonly GlobalKeyboardHook _keyboardHook;
    private readonly ImeStateDetector _imeDetector;
    private readonly TriggerDetector _triggerDetector;
    private readonly TextExpander _textExpander;
    private readonly SnippetRepository _snippetRepository;
    private readonly ExcludedAppRepository _excludedAppRepository;
    private readonly SettingsRepository _settingsRepository;

    private bool _isPaused;
    private bool _isExpanding; // 展開中フラグ（自前の入力を無視する）
    private HashSet<string> _excludedProcesses = new();

    /// <summary>展開成功時イベント</summary>
    public event Action<Snippet>? SnippetExpanded;

    /// <summary>一時停止状態変更イベント</summary>
    public event Action<bool>? PauseStateChanged;

    /// <summary>ステータスメッセージイベント</summary>
    public event Action<string>? StatusMessage;

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            _isPaused = value;
            PauseStateChanged?.Invoke(value);
            StatusMessage?.Invoke(value ? "一時停止中" : "監視中");
        }
    }

    public ExpansionService(
        GlobalKeyboardHook keyboardHook,
        ImeStateDetector imeDetector,
        TriggerDetector triggerDetector,
        TextExpander textExpander,
        SnippetRepository snippetRepository,
        ExcludedAppRepository excludedAppRepository,
        SettingsRepository settingsRepository)
    {
        _keyboardHook = keyboardHook;
        _imeDetector = imeDetector;
        _triggerDetector = triggerDetector;
        _textExpander = textExpander;
        _snippetRepository = snippetRepository;
        _excludedAppRepository = excludedAppRepository;
        _settingsRepository = settingsRepository;

        // トリガー一致時の処理
        _triggerDetector.TriggerMatched += OnTriggerMatched;
    }

    public void Start()
    {
        // 除外アプリリストを読み込み
        RefreshExcludedApps();

        // キーボードフックのイベント登録
        _keyboardHook.KeyDown += OnKeyDown;
        _keyboardHook.Install();

        _isPaused = _settingsRepository.GetBool("is_paused");
        StatusMessage?.Invoke(_isPaused ? "一時停止中" : "監視中");
    }

    public void Stop()
    {
        _keyboardHook.KeyDown -= OnKeyDown;
        _keyboardHook.Uninstall();
    }

    public void RefreshExcludedApps()
    {
        _excludedProcesses = _excludedAppRepository.GetProcessNames();
    }

    private void OnKeyDown(uint vkCode, bool isInjected)
    {
        // 自前の入力は無視
        if (isInjected || _isExpanding) return;

        // 一時停止中
        if (_isPaused) return;

        // 除外アプリ判定
        var processName = _imeDetector.GetForegroundProcessName();
        if (_excludedProcesses.Contains(processName)) return;

        // パスワードフィールド判定
        if (_imeDetector.IsPasswordField()) return;

        // IME未確定中は入力をバッファに追加しない
        if (_imeDetector.IsComposing()) return;

        // キー処理
        switch (vkCode)
        {
            case NativeMethods.VK_SPACE:
            case NativeMethods.VK_TAB:
                // トリガー判定
                _triggerDetector.CheckTrigger();
                break;

            case NativeMethods.VK_BACK:
                _triggerDetector.RemoveLastChar();
                break;

            case NativeMethods.VK_RETURN:
            case NativeMethods.VK_ESCAPE:
                // Enter/Escでバッファクリア
                _triggerDetector.ClearBuffer();
                break;

            default:
                // 文字キーの場合、バッファに追加
                var c = VkCodeToChar(vkCode);
                if (c.HasValue)
                {
                    _triggerDetector.AddChar(c.Value);
                }
                else
                {
                    // 方向キーなど制御キーの場合はバッファクリア
                    if (IsNavigationKey(vkCode))
                    {
                        _triggerDetector.ClearBuffer();
                    }
                }
                break;
        }
    }

    private async void OnTriggerMatched(Snippet snippet, int triggerLength)
    {
        if (_isExpanding) return;

        try
        {
            _isExpanding = true;
            StatusMessage?.Invoke($"展開中: {snippet.Title}");

            await _textExpander.ExpandAsync(snippet.Body, triggerLength);

            // 使用回数を記録
            var appName = _imeDetector.GetForegroundProcessName();
            _snippetRepository.RecordExpansion(snippet.Id, appName);

            SnippetExpanded?.Invoke(snippet);
            StatusMessage?.Invoke("監視中");
        }
        catch (Exception ex)
        {
            StatusMessage?.Invoke($"展開エラー: {ex.Message}");
        }
        finally
        {
            _isExpanding = false;
        }
    }

    /// <summary>
    /// 直接展開（検索ポップアップからの挿入用）
    /// </summary>
    public async Task ExpandDirectAsync(Snippet snippet)
    {
        if (_isExpanding) return;

        try
        {
            _isExpanding = true;

            var variableProcessor = new VariableProcessor();
            var (expandedText, cursorPosition) = variableProcessor.Expand(snippet.Body);

            // クリップボード経由で貼り付け
            System.Windows.Clipboard.SetText(expandedText);

            await Task.Delay(30);

            // Ctrl+V 送信
            SendCtrlV();

            // 使用回数を記録
            var appName = _imeDetector.GetForegroundProcessName();
            _snippetRepository.RecordExpansion(snippet.Id, appName);

            SnippetExpanded?.Invoke(snippet);
        }
        finally
        {
            _isExpanding = false;
        }
    }

    private void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_CONTROL, dwExtraInfo = GlobalKeyboardHook.InjectedMarker } }
        };
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_V, dwExtraInfo = GlobalKeyboardHook.InjectedMarker } }
        };
        inputs[2] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_V, dwFlags = NativeMethods.KEYEVENTF_KEYUP, dwExtraInfo = GlobalKeyboardHook.InjectedMarker } }
        };
        inputs[3] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION { ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_CONTROL, dwFlags = NativeMethods.KEYEVENTF_KEYUP, dwExtraInfo = GlobalKeyboardHook.InjectedMarker } }
        };
        NativeMethods.SendInput(4, inputs, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>
    /// 仮想キーコードを文字に変換（英数字のみ、日本語はIME確定後に別途処理）
    /// </summary>
    private static char? VkCodeToChar(uint vkCode)
    {
        // 英字 A-Z
        if (vkCode >= 0x41 && vkCode <= 0x5A)
        {
            bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            return shift ? (char)vkCode : (char)(vkCode + 32);
        }

        // 数字 0-9
        if (vkCode >= 0x30 && vkCode <= 0x39)
        {
            return (char)vkCode;
        }

        // テンキー 0-9
        if (vkCode >= 0x60 && vkCode <= 0x69)
        {
            return (char)(vkCode - 0x60 + '0');
        }

        // 注: 日本語入力の場合、VK_PROCESSKEYが送られてくる
        // 確定後の文字取得はWM_CHARメッセージで行う必要がある
        // WH_KEYBOARD_LL では直接取得できないため、
        // 日本語文字のバッファリングにはTextServicesFramework等の
        // 別メカニズムを使う設計とする

        return null;
    }

    private static bool IsNavigationKey(uint vkCode)
    {
        return vkCode is NativeMethods.VK_LEFT or NativeMethods.VK_RIGHT
            or NativeMethods.VK_UP or NativeMethods.VK_DOWN
            or 0x21 or 0x22 // Page Up/Down
            or 0x23 or 0x24; // End/Home
    }

    public void Dispose()
    {
        Stop();
    }
}
