using System.Threading;

namespace Yobidashi;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // 多重起動防止
        using var mutex = new Mutex(true, "Yobidashi_SingleInstance_{E3A7F1B2}", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
