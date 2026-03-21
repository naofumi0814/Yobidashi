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
    private readonly FocusedTextReader _focusedTextReader;

    private bool _isPaused;
    private volatile bool _isExpanding; // 展開中フラグ（自前の入力を無視する）
    private bool _wasComposing; // IME未確定状態の追跡
    private string _lastCompositionText = string.Empty; // 最後に取得した未確定文字列
    private string _compositionReading = string.Empty; // IME変換前のひらがな読み
    private HashSet<string> _excludedProcesses = new();

    // スキャンコード定数（物理キー識別用）
    private const uint SC_SPACE = 0x39;
    private const uint SC_TAB = 0x0F;

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
            _settingsRepository.SetBool("is_paused", value);
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
        SettingsRepository settingsRepository,
        FocusedTextReader focusedTextReader)
    {
        _keyboardHook = keyboardHook;
        _imeDetector = imeDetector;
        _triggerDetector = triggerDetector;
        _textExpander = textExpander;
        _snippetRepository = snippetRepository;
        _excludedAppRepository = excludedAppRepository;
        _settingsRepository = settingsRepository;
        _focusedTextReader = focusedTextReader;

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

    private void OnKeyDown(uint vkCode, uint scanCode, bool isInjected)
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

        // IME状態の判定
        bool isComposing = _imeDetector.IsComposing();

        if (isComposing)
        {
            // IME未確定中: 未確定文字列を追跡して保存
            var compText = _imeDetector.GetCompositionText();
            if (!string.IsNullOrEmpty(compText))
            {
                _lastCompositionText = compText;

                // ひらがな/カタカナのみの場合は読みとして保存
                // （変換後の漢字テキストで上書きしない）
                if (IsKanaOnly(compText))
                {
                    _compositionReading = compText;
                }
            }
            _wasComposing = true;
            return; // 未確定中はキー処理しない
        }

        // IME確定検出: 前回未確定中だったが今回は確定済み → テキストが確定された
        if (_wasComposing)
        {
            _wasComposing = false;

            // 確定文字列を取得
            var resultText = _imeDetector.GetResultText();
            var displayText = !string.IsNullOrEmpty(resultText) ? resultText : _lastCompositionText;

            // 読みテキスト: 保存されたひらがなを使用、なければ表示テキスト
            var readingText = !string.IsNullOrEmpty(_compositionReading) ? _compositionReading : displayText;

            if (!string.IsNullOrEmpty(displayText))
            {
                _triggerDetector.AddConfirmedText(displayText, readingText);
            }
            _lastCompositionText = string.Empty;
            _compositionReading = string.Empty;

            // Enter/Escでの確定の場合
            if (vkCode == NativeMethods.VK_RETURN)
            {
                return;
            }
            if (vkCode == NativeMethods.VK_ESCAPE)
            {
                _triggerDetector.ClearBuffer();
                return;
            }

            // VK_PROCESSKEYの場合、スキャンコードで実際のキーを判定
            // IME確定と同時に押されたキーがSpace/Tabならトリガー判定へ
            if (vkCode == NativeMethods.VK_PROCESSKEY)
            {
                if (scanCode == SC_SPACE || scanCode == SC_TAB)
                {
                    CheckTriggerWithScreenFallback();
                }
                return;
            }
            // Space/Tab等の場合はそのまま下のswitch文に流す（トリガー判定へ）
        }

        // VK_PROCESSKEYをスキャンコードで解決
        // IMEオン時、未確定なしでSpaceが押された場合に対応
        uint effectiveVk = vkCode;
        if (vkCode == NativeMethods.VK_PROCESSKEY)
        {
            effectiveVk = scanCode switch
            {
                SC_SPACE => NativeMethods.VK_SPACE,
                SC_TAB => NativeMethods.VK_TAB,
                _ => vkCode
            };
        }

        // キー処理
        switch (effectiveVk)
        {
            case NativeMethods.VK_SPACE:
            case NativeMethods.VK_TAB:
                // トリガー判定（バッファ → 画面テキストの順でフォールバック）
                CheckTriggerWithScreenFallback();
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
                // VK_PROCESSKEYのままの場合は無視
                if (effectiveVk == NativeMethods.VK_PROCESSKEY) break;

                // 文字キーの場合、バッファに追加
                var c = VkCodeToChar(effectiveVk);
                if (c.HasValue)
                {
                    _triggerDetector.AddChar(c.Value);
                }
                else
                {
                    // 制御キー（矢印等）の場合はバッファクリア
                    if (IsNavigationKey(effectiveVk))
                    {
                        _triggerDetector.ClearBuffer();
                    }
                }
                break;
        }
    }

    private async void OnTriggerMatched(Snippet snippet, int displayLength)
    {
        if (_isExpanding) return;

        try
        {
            _isExpanding = true;
            StatusMessage?.Invoke($"展開中: {snippet.Title}");

            await _textExpander.ExpandAsync(snippet.Body, displayLength);

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

            // クリップボード経由で貼り付け（UIスレッドで実行）
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                System.Windows.Clipboard.SetText(expandedText));

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

        // 記号キー（トリガー接頭辞で使用）
        // VK_OEM_1 = セミコロン/コロン (US: ;  JP: ;+)
        if (vkCode == 0xBA)
        {
            bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            return shift ? ':' : ';';
        }
        // VK_OEM_2 = スラッシュ/クエスチョン (US: /  JP: /)
        if (vkCode == 0xBF)
        {
            bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
            return shift ? '?' : '/';
        }

        return null;
    }

    private static bool IsNavigationKey(uint vkCode)
    {
        return vkCode is NativeMethods.VK_LEFT or NativeMethods.VK_RIGHT
            or NativeMethods.VK_UP or NativeMethods.VK_DOWN
            or 0x21 or 0x22 // Page Up/Down
            or 0x23 or 0x24; // End/Home
    }

    /// <summary>
    /// バッファ照合を試み、失敗したら画面テキスト(WM_GETTEXT)から照合する
    /// キーボードフックからはIME確定文字を取得できないため、
    /// 画面テキストからの直接照合が日本語トリガーの主要な検出手段となる
    /// </summary>
    private void CheckTriggerWithScreenFallback()
    {
        // 1. まずキーバッファからの照合を試行（ASCII入力向け）
        if (_triggerDetector.CheckTrigger()) return;

        // 2. バッファで見つからなかった場合、画面テキストから照合
        try
        {
            var textBeforeCursor = _focusedTextReader.ReadTextBeforeCursor();
            if (textBeforeCursor == null) return;

            var result = _triggerDetector.CheckTriggerFromScreenText(textBeforeCursor);
            if (result.HasValue)
            {
                _triggerDetector.ClearBuffer();
                OnTriggerMatched(result.Value.snippet, result.Value.displayLength);
            }
        }
        catch
        {
            // 画面テキスト取得失敗は静かに無視
        }
    }

    /// <summary>
    /// 文字列がひらがな/カタカナのみで構成されているか判定
    /// IME変換前の読み（ひらがな）を検出するために使用
    /// </summary>
    private static bool IsKanaOnly(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (var c in text)
        {
            if (!((c >= '\u3040' && c <= '\u309F') ||  // ひらがな
                  (c >= '\u30A0' && c <= '\u30FF') ||  // カタカナ
                  (c >= '\uFF65' && c <= '\uFF9F') ||  // 半角カタカナ
                  c == 'ー' || c == '～'))
            {
                return false;
            }
        }
        return true;
    }

    public void Dispose()
    {
        Stop();
    }
}
