using System.Runtime.InteropServices;
using System.Windows;
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
    /// 1. トリガー文字列 + トリガーキー を Backspace で削除
    /// 2. 変数を展開
    /// 3. 本文をクリップボード経由で貼り付け
    /// 4. カーソル位置を調整
    /// </summary>
    public async Task ExpandAsync(string body, int triggerLength)
    {
        int deleteCount = triggerLength + 1;
        SendBackspace(deleteCount);

        await Task.Delay(50);

        var (expandedText, cursorPosition) = _variableProcessor.Expand(body);

        await PasteTextAsync(expandedText);

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

    private void SendBackspace(int count)
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
                        wVk = NativeMethods.VK_BACK,
                        dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker
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
                        wVk = NativeMethods.VK_BACK,
                        dwFlags = NativeMethods.KEYEVENTF_KEYUP,
                        dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker
                    }
                }
            };
        }
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

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
                        dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker
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
                        dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker
                    }
                }
            };
        }
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private async Task PasteTextAsync(string text)
    {
        // 元のクリップボード内容を保存
        string? previousClipboard = null;
        try
        {
            if (Clipboard.ContainsText())
            {
                previousClipboard = Clipboard.GetText();
            }
        }
        catch { }

        // テキストをクリップボードにセット
        Clipboard.SetText(text);
        await Task.Delay(30);

        // Ctrl+V を送信
        SendCtrlV();

        // 元のクリップボード内容を復元
        await Task.Delay(100);
        if (previousClipboard != null)
        {
            try
            {
                Clipboard.SetText(previousClipboard);
            }
            catch { }
        }
    }

    private void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];

        inputs[0] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_CONTROL, dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker }
            }
        };
        inputs[1] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_V, dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker }
            }
        };
        inputs[2] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_V, dwFlags = NativeMethods.KEYEVENTF_KEYUP, dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker }
            }
        };
        inputs[3] = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.INPUTUNION
            {
                ki = new NativeMethods.KEYBDINPUT { wVk = NativeMethods.VK_CONTROL, dwFlags = NativeMethods.KEYEVENTF_KEYUP, dwExtraInfo = Core.Hooks.GlobalKeyboardHook.InjectedMarker }
            }
        };

        NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
