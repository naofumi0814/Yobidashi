using System.Runtime.InteropServices;
using Yobidashi.Interop;

namespace Yobidashi.Core.Expansion;

/// <summary>
/// テキスト展開エンジン
/// トリガー文字列を削除し、本文を挿入する
/// </summary>
public class TextExpander
{
    private readonly VariableProcessor _variableProcessor;

    public TextExpander(VariableProcessor variableProcessor)
    {
        _variableProcessor = variableProcessor;
    }

    /// <summary>
    /// テキスト展開を実行
    /// 1. トリガー文字列 + トリガーキー(Space/Tab) を Backspace で削除
    /// 2. 変数を展開
    /// 3. 本文をクリップボード経由で貼り付け
    /// 4. カーソル位置を調整
    /// </summary>
    public async Task ExpandAsync(string body, int triggerLength)
    {
        // トリガー文字列 + Space/Tab を削除（triggerLength + 1文字分）
        int deleteCount = triggerLength + 1;
        SendBackspace(deleteCount);

        // 少し待ってBackspaceの処理を完了させる
        await Task.Delay(50);

        // 変数展開
        var (expandedText, cursorPosition) = _variableProcessor.Expand(body);

        // クリップボード経由で貼り付け
        await PasteTextAsync(expandedText);

        // カーソル位置の調整
        if (cursorPosition >= 0)
        {
            await Task.Delay(30);
            int charsAfterCursor = expandedText.Length - cursorPosition;
            if (charsAfterCursor > 0)
            {
                SendLeftArrow(charsAfterCursor);
            }
        }
    }

    /// <summary>
    /// Backspaceキーを指定回数送信
    /// </summary>
    private void SendBackspace(int count)
    {
        var inputs = new NativeMethods.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            // KeyDown
            inputs[i * 2] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_BACK,
                        dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                    }
                }
            };
            // KeyUp
            inputs[i * 2 + 1] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_BACK,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                    }
                }
            };
        }
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>
    /// 左矢印キーを指定回数送信（カーソル位置調整用）
    /// </summary>
    private void SendLeftArrow(int count)
    {
        var inputs = new NativeMethods.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_LEFT,
                        dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                    }
                }
            };
            inputs[i * 2 + 1] = new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                u = new NativeMethods.INPUTUNION
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = NativeMethods.VK_LEFT,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                    }
                }
            };
        }
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    /// <summary>
    /// クリップボード経由でテキストを貼り付け
    /// 元のクリップボード内容を保存・復元する
    /// </summary>
    private async Task PasteTextAsync(string text)
    {
        // 現在のクリップボード内容を保存
        string? previousClipboard = null;
        try
        {
            var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                previousClipboard = await content.GetTextAsync();
            }
        }
        catch { }

        // テキストをクリップボードにセット
        var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dataPackage.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);

        await Task.Delay(30);

        // Ctrl+V を送信
        SendCtrlV();

        // 少し待ってから元のクリップボード内容を復元
        await Task.Delay(100);
        if (previousClipboard != null)
        {
            var restorePackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            restorePackage.SetText(previousClipboard);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(restorePackage);
        }
    }

    /// <summary>
    /// Ctrl+V を送信
    /// </summary>
    private void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];

        // Ctrl down
        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = NativeMethods.VK_CONTROL,
                    dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                }
            }
        };
        // V down
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = NativeMethods.VK_V,
                    dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                }
            }
        };
        // V up
        inputs[2] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = NativeMethods.VK_V,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                    dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                }
            }
        };
        // Ctrl up
        inputs[3] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = NativeMethods.VK_CONTROL,
                    dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                    dwExtraInfo = Hooks.GlobalKeyboardHook.InjectedMarker
                }
            }
        };

        NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
