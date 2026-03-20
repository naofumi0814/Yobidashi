using Microsoft.Win32;

namespace Yobidashi.Core.Services;

/// <summary>
/// Windows起動時の自動起動管理
/// </summary>
public class AutoStartManager
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Yobidashi";

    /// <summary>自動起動が有効かどうか</summary>
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>自動起動を有効にする</summary>
    public void Enable()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            key?.SetValue(AppName, $"\"{exePath}\" --minimized");
        }
        catch
        {
            // レジストリ書き込み失敗は無視（権限不足など）
        }
    }

    /// <summary>自動起動を無効にする</summary>
    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
            key?.DeleteValue(AppName, false);
        }
        catch
        {
            // 失敗は無視
        }
    }

    /// <summary>自動起動のオンオフを切り替え</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled) Enable();
        else Disable();
    }
}
