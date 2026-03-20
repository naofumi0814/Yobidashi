using System.Text;
using Yobidashi.Interop;

namespace Yobidashi.Core.Services;

/// <summary>
/// IME未確定状態の検知
/// 日本語変換中の文字列に誤反応しないようにする
/// </summary>
public class ImeStateDetector
{
    /// <summary>
    /// 現在のフォアグラウンドウィンドウでIME未確定文字列があるかどうか
    /// </summary>
    public bool IsComposing()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        // フォアグラウンドウィンドウのスレッドにアタッチしてIMEコンテキストを取得
        uint foregroundThreadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        uint currentThreadId = NativeMethods.GetCurrentThreadId();

        bool attached = false;
        if (foregroundThreadId != currentThreadId)
        {
            attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        try
        {
            var hIMC = NativeMethods.ImmGetContext(hwnd);
            if (hIMC == IntPtr.Zero) return false;

            try
            {
                // 未確定文字列の長さを取得
                int len = NativeMethods.ImmGetCompositionString(hIMC, NativeMethods.GCS_COMPSTR, null, 0);
                return len > 0;
            }
            finally
            {
                NativeMethods.ImmReleaseContext(hwnd, hIMC);
            }
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    /// <summary>
    /// IME未確定文字列（変換中のテキスト）を取得
    /// </summary>
    public string GetCompositionText()
    {
        return GetImeString(NativeMethods.GCS_COMPSTR);
    }

    /// <summary>
    /// IME確定文字列（確定されたテキスト）を取得
    /// </summary>
    public string GetResultText()
    {
        return GetImeString(NativeMethods.GCS_RESULTSTR);
    }

    private string GetImeString(uint dwIndex)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return string.Empty;

        uint foregroundThreadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        uint currentThreadId = NativeMethods.GetCurrentThreadId();

        bool attached = false;
        if (foregroundThreadId != currentThreadId)
        {
            attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        try
        {
            var hIMC = NativeMethods.ImmGetContext(hwnd);
            if (hIMC == IntPtr.Zero) return string.Empty;

            try
            {
                int len = NativeMethods.ImmGetCompositionString(hIMC, dwIndex, null, 0);
                if (len <= 0) return string.Empty;

                byte[] buf = new byte[len];
                NativeMethods.ImmGetCompositionString(hIMC, dwIndex, buf, (uint)len);
                return Encoding.Unicode.GetString(buf);
            }
            finally
            {
                NativeMethods.ImmReleaseContext(hwnd, hIMC);
            }
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    /// <summary>
    /// パスワードフィールドかどうかを検出
    /// </summary>
    public bool IsPasswordField()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        uint foregroundThreadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        uint currentThreadId = NativeMethods.GetCurrentThreadId();

        bool attached = false;
        if (foregroundThreadId != currentThreadId)
        {
            attached = NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        try
        {
            var focusHwnd = NativeMethods.GetFocus();
            if (focusHwnd == IntPtr.Zero) return false;

            // ウィンドウクラス名を確認
            var className = new StringBuilder(256);
            NativeMethods.GetClassName(focusHwnd, className, 256);
            var cls = className.ToString().ToLowerInvariant();

            // Edit コントロールのパスワードスタイルを確認
            if (cls == "edit")
            {
                int style = NativeMethods.GetWindowLong(focusHwnd, NativeMethods.GWL_STYLE);
                return (style & NativeMethods.ES_PASSWORD) != 0;
            }

            // Chrome/Edgeなどのパスワードフィールドはクラス名だけでは判定困難
            // UIA等を使う必要があるが、MVPでは最低限のEdit検出のみ
            return false;
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    /// <summary>
    /// フォアグラウンドアプリのプロセス名を取得
    /// </summary>
    public string GetForegroundProcessName()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return string.Empty;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        try
        {
            var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName.ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
}
