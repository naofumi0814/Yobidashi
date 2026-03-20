using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Yobidashi;

/// <summary>
/// プログラムエントリポイント
/// </summary>
public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // 多重起動防止
        using var mutex = new Mutex(true, "Yobidashi_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            // 既に起動済み
            return;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
