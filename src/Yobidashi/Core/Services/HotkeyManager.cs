using Yobidashi.Interop;

namespace Yobidashi.Core.Services;

/// <summary>
/// グローバルホットキー管理
/// </summary>
public class HotkeyManager : IDisposable
{
    private IntPtr _windowHandle;
    private readonly Dictionary<int, Action> _registeredHotkeys = new();
    private int _nextId = 1;

    public void Initialize(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// ホットキーを登録
    /// </summary>
    public int Register(uint modifiers, uint vk, Action callback)
    {
        int id = _nextId++;
        if (NativeMethods.RegisterHotKey(_windowHandle, id, modifiers | NativeMethods.MOD_NOREPEAT, vk))
        {
            _registeredHotkeys[id] = callback;
            return id;
        }
        return -1;
    }

    /// <summary>
    /// ホットキーメッセージを処理
    /// </summary>
    public void HandleHotkeyMessage(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var callback))
        {
            callback.Invoke();
        }
    }

    /// <summary>
    /// 全てのホットキーを解除
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }
        _registeredHotkeys.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
    }
}
