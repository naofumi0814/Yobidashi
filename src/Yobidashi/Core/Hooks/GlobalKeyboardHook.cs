using System.Diagnostics;
using System.Runtime.InteropServices;
using Yobidashi.Interop;

namespace Yobidashi.Core.Hooks;

/// <summary>
/// グローバルキーボードフック（WH_KEYBOARD_LL）
/// 全てのキー入力を監視し、イベントを発行する
/// </summary>
public class GlobalKeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private bool _disposed;

    /// <summary>キーダウンイベント（vkCode, isInjected）</summary>
    public event Action<uint, bool>? KeyDown;

    /// <summary>キーアップイベント（vkCode）</summary>
    public event Action<uint>? KeyUp;

    /// <summary>フック処理中にtrueを返すとキー入力を抑制する</summary>
    public Func<uint, int, bool>? ShouldSuppress { get; set; }

    // 自前のSendInputで送った入力を識別するためのマーカー
    public static readonly IntPtr InjectedMarker = (IntPtr)0x594F4249; // "YOBI"

    public GlobalKeyboardHook()
    {
        _proc = HookCallback;
    }

    public void Install()
    {
        if (_hookId != IntPtr.Zero) return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(curModule.ModuleName),
            0);

        if (_hookId == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"キーボードフックの設定に失敗しました。エラーコード: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;
            bool isInjected = hookStruct.dwExtraInfo == InjectedMarker;

            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                // 抑制判定
                if (ShouldSuppress?.Invoke(hookStruct.vkCode, msg) == true)
                {
                    return (IntPtr)1; // キー入力を抑制
                }

                KeyDown?.Invoke(hookStruct.vkCode, isInjected);
            }
            else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
            {
                KeyUp?.Invoke(hookStruct.vkCode);
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Uninstall();
            _disposed = true;
        }
    }
}
