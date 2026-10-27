// Clothesline — 把截图挂在屏幕顶部一条绳上的小工具（Windows 版）。
// 改编自 Tendedero (macOS) by Alejandro Buján — https://github.com/alejandrobujan/tendedero
// 原作为 MIT 许可；本项目是行为对齐的独立重写（C# / WPF），不含原作源代码，
// 交互设计与功能定义均致敬原作者。完整版权声明见 LICENSE。

using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace Clothesline;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        _mutex = new Mutex(true, "Clothesline_SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("Clothesline 已经在运行了（在任务栏托盘里找晾衣夹图标）。", "Clothesline", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clothesline");
        Directory.CreateDirectory(dir);
        string logPath = Path.Combine(dir, "log.txt");
        void Log(string msg) { try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\r\n"); } catch { } }
        Log("启动");

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log($"FATAL: {e.ExceptionObject}");
        System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (_, e) => { Log($"Dispatcher: {e.Exception}"); e.Handled = true; };

        var settings = Settings.Load();

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Log($"Dispatcher: {e.Exception}"); e.Handled = true; };

        var win = new LineWindow(settings);
        win.Show();

        var tray = new TrayIcon(win, settings);
        win.AttachTray(tray);
        win.StartWatchers();

        app.Run();

        win.Cleanup();
        tray.Dispose();
        Log("退出");
        try { _mutex.ReleaseMutex(); } catch { }
    }
}
