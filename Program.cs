// Clothesline — 把截图挂在屏幕顶部一条绳上的小工具（Windows 版）。
// 改编自 Tendedero (macOS) by Alejandro Buján — https://github.com/alejandrobujan/tendedero
// 原作为 MIT 许可；本项目是行为对齐的独立重写（C# / WPF），不含原作源代码，
// 交互设计与功能定义均致敬原作者。完整版权声明见 LICENSE。

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace Clothesline;

internal static class Program
{
    private const string MutexName = "Clothesline_SingleInstance";
    private const string PipeName = "Clothesline_IPC";

    private static Mutex? _mutex;

    [STAThread]
    private static void Main(string[] args)
    {
        List<string> files = CollectFiles(args);
        bool wantGuide = args.Any(a => a.Equals("--guide", StringComparison.OrdinalIgnoreCase));

        _mutex = new Mutex(true, MutexName, out bool created);
        if (!created)
        {
            // 已在运行：把文件转交给它（没有文件就让绳子滑下来打个招呼），然后本进程退出
            if (!Forward(files, wantGuide))
            {
                MessageBox.Show(
                    "Clothesline 已经在运行了。\n\n它没有主窗口，请看屏幕右下角任务栏托盘里的「晾衣夹」图标——左键单击可让绳子滑下来，右键有菜单。",
                    "Clothesline", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }

        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clothesline");
        Directory.CreateDirectory(dir);
        string logPath = Path.Combine(dir, "log.txt");
        void Log(string msg) { try { File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\r\n"); } catch { } }
        Log($"启动（参数 {files.Count} 个文件）");

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

        StartPipeServer(win, settings, Log);

        // 每次启动校正一次系统集成状态（exe 可能被搬过位置）
        ShellMenu.Apply(settings.ContextMenuEnabled);
        if (settings.RunAtStartup) TrayIcon.SetStartup(true);

        // 首启引导：让第一次打开的人知道这东西在哪、怎么用
        app.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (files.Count > 0) win.HangPaths(files);
            if (!settings.FirstRunDone || wantGuide)
            {
                settings.FirstRunDone = true;
                settings.Save();
                Log(wantGuide ? "按请求显示使用说明" : "首次启动：显示使用说明");
                OnboardingWindow.ShowFor(win, settings);
            }
        }));

        app.Run();

        win.Cleanup();
        tray.Dispose();
        Log("退出");
        try { _mutex.ReleaseMutex(); } catch { }
    }

    /// <summary>从命令行里挑出真实存在的文件（右键菜单传 --hang "路径"，也可直接把文件拖到 exe 上）。</summary>
    private static List<string> CollectFiles(string[] args)
    {
        var list = new List<string>();
        if (args.Length == 0) return list;
        foreach (string a in args)
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            if (a.StartsWith("--", StringComparison.Ordinal)) continue; // --hang 等开关
            try
            {
                string p = a.Trim().Trim('"');
                if (File.Exists(p)) list.Add(p);
            }
            catch { }
        }
        return list;
    }

    /// <summary>把文件（或空列表=请求唤出）转交给已运行的实例；失败返回 false。</summary>
    private static bool Forward(List<string> files, bool wantGuide)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2500);
            using var w = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            var lines = new List<string>(files);
            if (wantGuide) lines.Add("--guide");
            w.Write(string.Join("\n", lines));
            return true;
        }
        catch (Exception e)
        {
            Diag.Log("转发到已运行实例失败：" + e.Message);
            return false;
        }
    }

    /// <summary>常驻管道服务：接收后续实例传来的文件列表。</summary>
    private static void StartPipeServer(LineWindow win, Settings settings, Action<string> log)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.None);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server, new UTF8Encoding(false));
                    string text = reader.ReadToEnd();
                    bool guide = text.Contains("--guide", StringComparison.Ordinal);
                    List<string> list = text
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0 && File.Exists(s))
                        .ToList();

                    win.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (list.Count > 0) win.HangPaths(list);
                        if (guide) OnboardingWindow.ShowFor(win, settings);
                        else if (list.Count == 0) win.Peek(3);
                    }));
                    log($"IPC：收到 {list.Count} 个文件{(guide ? "（含说明请求）" : "")}");
                }
                catch (Exception e)
                {
                    log("IPC 异常：" + e.Message);
                    Thread.Sleep(400);
                }
            }
        })
        { IsBackground = true, Name = "ClotheslineIPC" };
        t.Start();
    }
}
