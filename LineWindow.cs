// 顶部晾绳面板。行为规范移植自 Tendedero (macOS) by Alejandro Buján —
// https://github.com/alejandrobujan/tendedero（MIT，版权归原作者）。详见 LICENSE。

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;
using System.Windows.Threading;

namespace Clothesline;

/// <summary>屏幕顶部的晾绳面板：透明、置顶、不抢焦点；鼠标触顶滑落，离开收起。</summary>
internal sealed class LineWindow : Window
{
    internal const double StripH = 160;

    private readonly Settings _settings;
    private readonly TranslateTransform _slide = new();
    private readonly List<HangItem> _items = new();
    private readonly MouseHook _mouse = new();
    private readonly ScreenshotWatcher _watcher;
    private TrayIcon? _tray;

    private Grid _root = null!;
    private StackPanel _row = null!;
    private System.Windows.Shapes.Path _line = null!;

    private IntPtr _hwnd;
    private uint _taskbarCreated;
    private bool _pinned, _shown;
    private uint _exclClipFmt;
    private long _suppressClipboardUntil;
    private (DateTime t, int w, int h)? _lastFileItem;
    private DispatcherTimer? _hideTimer, _peekTimer, _breezeTimer;

    public bool IsPinned => _pinned;

    public LineWindow(Settings settings)
    {
        _settings = settings;
        _watcher = new ScreenshotWatcher(OnNewFile);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        Title = "Clothesline";
        Width = SystemParameters.PrimaryScreenWidth;
        Height = StripH;
        Left = 0;
        Top = 0;

        BuildVisual();
        _slide.Y = -StripH; // 收起

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); if (!_pinned && _shown) HideLine(); };

        MouseEnter += (_, _) => _hideTimer.Stop();
        MouseLeave += (_, _) => { if (_shown && !_pinned) _hideTimer.Start(); };

        Loaded += (_, _) => DrawLine();
        SourceInitialized += OnSourceInitialized;
    }

    // ---------- 界面 ----------

    private void BuildVisual()
    {
        _root = new Grid { Background = Brushes.Transparent, RenderTransform = _slide };

        _line = new System.Windows.Shapes.Path
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xA1, 0x84, 0x5E)),
            StrokeThickness = 2,
        };
        _root.Children.Add(_line);

        var poleBrush = new LinearGradientBrush(Color.FromRgb(0x8B, 0x5E, 0x3C), Color.FromRgb(0x5E, 0x3E, 0x25), 90);
        _root.Children.Add(new Rectangle
        {
            Width = 6, Height = 34, RadiusX = 3, RadiusY = 3, Fill = poleBrush,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(3, 4, 0, 0),
        });
        _root.Children.Add(new Rectangle
        {
            Width = 6, Height = 34, RadiusX = 3, RadiusY = 3, Fill = poleBrush,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 3, 0),
        });

        _row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 22, 0, 0),
        };
        _root.Children.Add(_row);

        Content = _root;
    }

    private void DrawLine()
    {
        double w = ActualWidth > 0 ? ActualWidth : Width;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(0, 30), isFilled: false, isClosed: false);
            ctx.QuadraticBezierTo(new Point(w / 2, 37), new Point(w, 30), isStroked: true, isSmoothJoin: false);
        }
        g.Freeze();
        _line.Data = g;
    }

    // ---------- Win32 接线 ----------

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);

        int ex = unchecked((int)Native.GetWindowLongPtrW(_hwnd, Native.GWL_EXSTYLE).ToInt64());
        ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
        Native.SetWindowLongPtrW(_hwnd, Native.GWL_EXSTYLE, ex);

        SetClickThrough(true);

        Native.RegisterHotKey(_hwnd, 1, Native.MOD_CONTROL | Native.MOD_ALT, 0x54 /* T */);
        Native.AddClipboardFormatListener(_hwnd);
        _exclClipFmt = Native.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        _taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");

        _mouse.MouseMoved += OnGlobalMouse;
        _mouse.Install();

        _breezeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _breezeTimer.Tick += (_, _) =>
        {
            _breezeTimer.Interval = TimeSpan.FromSeconds(4 + Random.Shared.NextDouble() * 10);
            if (_items.Count > 0) _items[Random.Shared.Next(_items.Count)].Breeze();
        };
        _breezeTimer.Start();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // 资源管理器重启广播（消息号是运行时注册的，不能写死常量）
        if (_taskbarCreated != 0 && msg == (int)_taskbarCreated)
        {
            _tray?.ReAdd();
            handled = true;
            return IntPtr.Zero;
        }

        switch (msg)
        {
            case Native.WM_MOUSEACTIVATE:
                handled = true;
                return (IntPtr)Native.MA_NOACTIVATE;
            case Native.WM_HOTKEY:
                if (wParam == (IntPtr)1) { TogglePin(); handled = true; }
                break;
            case Native.WM_CLIPBOARDUPDATE:
                OnClipboardChanged();
                break;
            case Native.WM_APP + 1:
                _tray?.HandleTrayMessage(unchecked((int)lParam.ToInt64()));
                handled = true;
                return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    private void SetClickThrough(bool on)
    {
        if (_hwnd == IntPtr.Zero) return;
        int ex = unchecked((int)Native.GetWindowLongPtrW(_hwnd, Native.GWL_EXSTYLE).ToInt64());
        if (on) ex |= Native.WS_EX_TRANSPARENT;
        else ex &= ~Native.WS_EX_TRANSPARENT;
        Native.SetWindowLongPtrW(_hwnd, Native.GWL_EXSTYLE, ex);
    }

    // ---------- 显示 / 收起 ----------

    private void OnGlobalMouse(int x, int y)
    {
        if (_shown || _pinned) return;
        if (y > 2) return; // 未触顶
        int w = Native.GetSystemMetrics(0);
        if (x < 0 || x > w) return; // 只管主屏
        if (IsForegroundFullscreen()) return;
        Dispatcher.BeginInvoke(() => { if (!_shown && !_pinned) ShowLine(); });
    }

    private static bool IsForegroundFullscreen()
    {
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var sb = new System.Text.StringBuilder(64);
        Native.GetClassName(fg, sb, 64);
        var cls = sb.ToString();
        if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!Native.GetWindowRect(fg, out var r)) return false;
        int w = Native.GetSystemMetrics(0), h = Native.GetSystemMetrics(1);
        return r.Left <= 0 && r.Top <= 0 && r.Right >= w && r.Bottom >= h;
    }

    private void ShowLine()
    {
        _shown = true;
        _hideTimer.Stop();
        SetClickThrough(false);
        SlideTo(0, TimeSpan.FromSeconds(0.55), new BounceEase { Bounces = 1, Bounciness = 3, EasingMode = EasingMode.EaseOut });
    }

    private void HideLine()
    {
        _shown = false;
        SetClickThrough(true);
        SlideTo(-StripH, TimeSpan.FromSeconds(0.3), new CubicEase { EasingMode = EasingMode.EaseIn });
    }

    private void SlideTo(double to, TimeSpan dur, IEasingFunction ease)
    {
        double cur = _slide.Y; // 读取当前（含动画）值，避免收起/落下切换时跳变
        _slide.BeginAnimation(TranslateTransform.YProperty, null);
        _slide.Y = cur;
        var a = new DoubleAnimation(to, dur) { EasingFunction = ease, FillBehavior = FillBehavior.HoldEnd };
        _slide.BeginAnimation(TranslateTransform.YProperty, a);
    }

    public void TogglePin()
    {
        if (_pinned)
        {
            _pinned = false;
            _hideTimer.Stop();
            if (_shown) HideLine();
        }
        else
        {
            _pinned = true;
            if (!_shown) ShowLine();
        }
    }

    /// <summary>滑下绳子并保持若干秒后自动收起（给说明窗"试一试"和右键菜单用）。</summary>
    public void Peek(int seconds)
    {
        if (_pinned) return;
        ShowLine();
        _peekTimer ??= new DispatcherTimer();
        _peekTimer.Stop();
        _peekTimer.Tick -= OnPeekTick;
        _peekTimer.Tick += OnPeekTick;
        _peekTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, seconds));
        _peekTimer.Start();
    }

    /// <summary>把外部文件（右键菜单 / 命令行 / 已运行实例转发）挂到绳上。</summary>
    public void HangPaths(IEnumerable<string> paths)
    {
        int n = 0;
        foreach (string p in paths)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(p) || !File.Exists(p)) continue;
                string ext = Path.GetExtension(p).ToLowerInvariant();
                bool video = ext is ".mp4" or ".mov" or ".m4v" or ".mkv" or ".avi" or ".wmv" or ".webm";
                AddItem(p, video);
                n++;
            }
            catch (Exception e) { Diag.Log($"挂载外部文件失败（{p}）：{e.Message}"); }
        }
        Diag.Log($"外部挂载：{n} 个");
        if (n > 0) Peek(4);
    }

    private void PeekBriefly()
    {
        ShowLine();
        _peekTimer ??= new DispatcherTimer();
        _peekTimer.Stop();
        _peekTimer.Tick -= OnPeekTick;
        _peekTimer.Tick += OnPeekTick;
        _peekTimer.Interval = TimeSpan.FromSeconds(1.8);
        _peekTimer.Start();
    }

    private void OnPeekTick(object? sender, EventArgs e)
    {
        _peekTimer!.Stop();
        if (_pinned) return;
        if (IsMouseInsideStrip()) { _peekTimer.Start(); return; }
        if (_shown) HideLine();
    }

    private bool IsMouseInsideStrip()
    {
        if (!Native.GetCursorPos(out var p)) return true;
        double scale = _hwnd != IntPtr.Zero ? Native.GetDpiForWindow(_hwnd) / 96.0 : 1.0;
        return p.X >= 0 && p.X <= Width * scale && p.Y >= 0 && p.Y <= StripH * scale;
    }

    // ---------- 挂图 ----------

    public void AttachTray(TrayIcon tray)
    {
        _tray = tray;
        tray.SetCount(_items.Count);
    }

    public void StartWatchers() => _watcher.Start();

    private void OnNewFile(string path, bool isVideo)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try { AddItem(path, isVideo); } catch { }
        });
    }

    public void AddItem(string path, bool isVideo, ImageSource? preview = null)
    {
        ImageSource? thumb = preview;
        if (thumb == null && !isVideo) thumb = LoadThumbnail(path);
        if (thumb == null && !isVideo) return; // 图片读不出来就不挂

        var item = new HangItem(path, isVideo, thumb);
        HookItem(item);
        _row.Children.Add(item.Visual);
        item.Visual.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromSeconds(0.3)));
        item.Swing((Random.Shared.NextDouble() - 0.5) * 14);
        _items.Add(item);

        if (preview == null && !isVideo && thumb is BitmapSource bs)
            _lastFileItem = (DateTime.Now, bs.PixelWidth, bs.PixelHeight);

        TrimOverflow();
        _tray?.SetCount(_items.Count);

        if (!_shown && !_pinned) PeekBriefly(); // 新截图来了，露个头提示
    }

    private static ImageSource? LoadThumbnail(string path)
    {
        try
        {
            var di = new BitmapImage();
            di.BeginInit();
            di.CacheOption = BitmapCacheOption.OnLoad;
            di.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            di.DecodePixelWidth = 320;
            di.UriSource = new Uri(path);
            di.EndInit();
            di.Freeze();
            return di;
        }
        catch { return null; }
    }

    private void HookItem(HangItem item)
    {
        item.CopyClick += it =>
        {
            try
            {
                var bmp = BitmapFrame.Create(new Uri(it.FilePath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                _suppressClipboardUntil = Environment.TickCount64 + 1200; // 别把自己的复制又挂上去
                Clipboard.SetImage(bmp);
                it.FlashCopied();
            }
            catch { }
        };
        item.OpenClick += it =>
        {
            try { _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(it.FilePath) { UseShellExecute = true }); } catch { }
        };
        item.UnhangClick += Unhang;
        item.DeleteClick += it => { if (DeleteToRecycleBin(it.FilePath)) Unhang(it); };
    }

    private void Unhang(HangItem it)
    {
        _items.Remove(it);
        _row.Children.Remove(it.Visual);
        _tray?.SetCount(_items.Count);
    }

    private void TrimOverflow()
    {
        int max = (int)((Width - 20) / 174);
        if (max < 3) max = 3;
        if (max > 14) max = 14;
        while (_items.Count > max)
        {
            var old = _items[0];
            _items.RemoveAt(0);
            _row.Children.Remove(old.Visual);
        }
    }

    private static bool DeleteToRecycleBin(string path)
    {
        var op = new Native.SHFILEOPSTRUCTW
        {
            wFunc = Native.FO_DELETE,
            pFrom = path + "\0", // 双 null 结尾
            fFlags = Native.FOF_ALLOWUNDO | Native.FOF_NOCONFIRMATION | Native.FOF_SILENT | Native.FOF_NOERRORUI,
        };
        return Native.SHFileOperation(ref op) == 0 && !op.fAnyOperationsAborted;
    }

    // ---------- 剪贴板 ----------

    private void OnClipboardChanged()
    {
        if (!_settings.HangCopiedImages) return;
        if (Environment.TickCount64 < _suppressClipboardUntil) return;
        try
        {
            if (Clipboard.ContainsText() || Clipboard.ContainsFileDropList() || Clipboard.ContainsAudio()) return;
            if (!Clipboard.ContainsImage()) return;
            if (_exclClipFmt != 0 && Native.IsClipboardFormatAvailable(_exclClipFmt)) return; // 密码管理器标记的私有内容
            var img = Clipboard.GetImage();
            if (img == null || img.PixelWidth < 8 || img.PixelHeight < 8) return;
            if (_lastFileItem is { } last
                && (DateTime.Now - last.t).TotalSeconds < 5
                && last.w == img.PixelWidth && last.h == img.PixelHeight) return; // Win+Shift+S 已按文件挂过，去重

            var store = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clothesline", "Hang");
            Directory.CreateDirectory(store);
            var file = Path.Combine(store, $"copy_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using (var fs = File.Create(file)) enc.Save(fs);
            AddItem(file, false, img);
        }
        catch { }
    }

    // ---------- 退出清理 ----------

    public void RequestExit() => Application.Current.Shutdown();

    public void Cleanup()
    {
        try { if (_hwnd != IntPtr.Zero) Native.UnregisterHotKey(_hwnd, 1); } catch { }
        try { if (_hwnd != IntPtr.Zero) Native.RemoveClipboardFormatListener(_hwnd); } catch { }
        _mouse.Dispose();
        _watcher.Dispose();
        _breezeTimer?.Stop();
        _hideTimer?.Stop();
        _peekTimer?.Stop();
    }
}
