using System.Text;
using Yobidashi.Interop;

namespace Yobidashi.Core.Services;

/// <summary>
/// フォーカスされたコントロールから実際のテキストを読み取る
/// WM_GETTEXT / EM_GETSEL を使用し、標準Win32コントロール（メモ帳等）で動作する
/// IME経由の日本語テキストもコントロールに確定済みであれば読み取れる
/// </summary>
public class FocusedTextReader
{
    /// <summary>
    /// フォーカスされたコントロールのカーソル直前のテキストを取得する
    /// </summary>
    /// <returns>カーソル前のテキスト。取得できない場合はnull</returns>
    public string? ReadTextBeforeCursor()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;

        uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();

        bool attached = false;
        if (threadId != currentThread)
        {
            attached = NativeMethods.AttachThreadInput(currentThread, threadId, true);
        }

        try
        {
            var focus = NativeMethods.GetFocus();
            if (focus == IntPtr.Zero) return null;

            // テキスト長を取得
            int len = NativeMethods.SendMessage(focus, NativeMethods.WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero).ToInt32();
            if (len <= 0 || len > 50000) return null;

            // テキストを取得
            var buffer = new StringBuilder(len + 1);
            NativeMethods.SendMessageText(focus, NativeMethods.WM_GETTEXT, (IntPtr)(len + 1), buffer);
            var text = buffer.ToString();

            // カーソル位置を取得（EM_GETSEL）
            NativeMethods.SendMessageGetSel(focus, NativeMethods.EM_GETSEL, out int selStart, out int selEnd);

            // EM_GETSELが有効な値を返さなかった場合、末尾をカーソル位置とみなす
            if (selStart <= 0 || selStart > text.Length)
            {
                selStart = text.Length;
            }

            return text[..selStart];
        }
        catch
        {
            return null;
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, threadId, false);
            }
        }
    }
}
