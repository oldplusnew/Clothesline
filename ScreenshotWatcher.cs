using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Clothesline;

/// <summary>监控截图文件夹与录屏文件夹，等文件写完后回调（在后台线程触发）。</summary>
internal sealed class ScreenshotWatcher : IDisposable
{
    private readonly Action<string, bool> _onReady; // (路径, 是否视频) —— 线程池回调
    private readonly HashSet<string> _recent = new();
    private readonly List<FileSystemWatcher> _watchers = new();

    public ScreenshotWatcher(Action<string, bool> onReady)
    {
        _onReady = onReady;
    }

    public void Start()
    {
        var pics = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        Watch(pics, video: false); // Win+PrtScn、截图工具保存位置

        var caps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Captures");
        if (!string.Equals(caps, pics, StringComparison.OrdinalIgnoreCase))
            Watch(caps, video: true); // Win+G 录屏保存位置
    }

    private void Watch(string dir, bool video)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var fsw = new FileSystemWatcher(dir)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };
            fsw.Created += (_, e) => Enqueue(e.FullPath, video);
            fsw.Renamed += (_, e) => Enqueue(e.FullPath, video);
            _watchers.Add(fsw);
        }
        catch { /* 无权限或路径异常则放弃该目录 */ }
    }

    private void Enqueue(string path, bool video)
    {
        lock (_recent)
        {
            if (!_recent.Add(path)) return;
            if (_recent.Count > 500) _recent.Clear();
        }

        var ext = (Path.GetExtension(path) ?? "").ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".mp4")) return;

        var t = new Thread(() =>
        {
            for (int i = 0; i < 25; i++) // 最多等 ~3.7s 文件写完
            {
                try
                {
                    if (!File.Exists(path)) return;
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (fs.Length > 0)
                    {
                        _onReady(path, video);
                        return;
                    }
                }
                catch { }
                Thread.Sleep(150);
            }
        })
        { IsBackground = true };
        t.Start();
    }

    public void Dispose()
    {
        foreach (var w in _watchers)
        {
            try { w.EnableRaisingEvents = false; w.Dispose(); } catch { }
        }
        _watchers.Clear();
    }
}
