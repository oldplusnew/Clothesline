// 顶部晾绳面板。行为规范移植自 Tendedero (macOS) by Alejandro Buján —
// https://github.com/alejandrobujan/tendedero（MIT，版权归原作者）。详见 LICENSE。

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private Border _dropHint = null!;
    private Border _pinBtn = null!;
    private Ellipse _pinHead = null!;
    private System.Windows.Shapes.Path _pinBody = null!;

    private IntPtr _hwnd;
    private uint _taskbarCreated;
    private bool _pinned, _shown;
    /// <summary>常驻模式下，鼠标此刻是否在绳区（在就不穿透，好让人点图和图钉）。</summary>
    private bool _stripInteractive;
    private uint _exclClipFmt;
    private long _suppressClipboardUntil;
    private (DateTime t, int w, int h)? _lastFileItem;
    private DispatcherTimer? _hideTimer, _peekTimer, _breezeTimer, _dragGuard;

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
        HookDrop();
        _slide.Y = -StripH; // 收起

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); if (!_pinned && _shown) HideLine(); };

        MouseEnter += (_, _) => _hideTimer.Stop();
        MouseLeave += (_, _) => { if (_shown && !_pinned) _hideTimer.Start(); };

        Loaded += (_, _) => { DrawLine(); ApplyPinnedFromSettings(); };
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

        // 拖放时的提示：只在有文件拖到绳上时出现
        _dropHint = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xD8, 0x3A, 0x33, 0x2A)),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(14, 6, 14, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 92, 0, 0),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "松手，挂到绳上",
                FontSize = 12,
                Foreground = Brushes.White,
            },
        };
        _root.Children.Add(_dropHint);
        BuildPinButton();

        Content = _root;
    }

    /// <summary>绳子右端下方的图钉按钮：按下把晾绳固定在桌面，再按一次收起。</summary>
    private void BuildPinButton()
    {
        var body = new StreamGeometry();
        using (var ctx = body.Open())
        {
            ctx.BeginFigure(new Point(8.4, 12.6), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(13.6, 12.6), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(11, 21.6), isStroked: true, isSmoothJoin: false);
        }
        body.Freeze();

        _pinHead = new Ellipse
        {
            Width = 12, Height = 12,
            StrokeThickness = 1.2,
            Stroke = new SolidColorBrush(Colors.White),
        };
        Canvas.SetLeft(_pinHead, 5);
        Canvas.SetTop(_pinHead, 1.5);

        _pinBody = new System.Windows.Shapes.Path
        {
            Data = body,
            StrokeThickness = 1,
            Stroke = new SolidColorBrush(Colors.White),
            StrokeLineJoin = PenLineJoin.Round,
        };

        var canvas = new Canvas { Width = 22, Height = 23 };
        canvas.Children.Add(_pinBody);
        canvas.Children.Add(_pinHead);

        _pinBtn = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(13),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            // 刻意不放在右上角：那儿正是最大化窗口「关闭」按钮的位置，会挡住红叉。
            // 挪到绳子下方（y≈40），避开顶部标题栏那条危险带，又还挨着绳子。
            Margin = new Thickness(0, 40, 16, 0),
            Cursor = Cursors.Hand,
            Child = canvas,
        };
        _pinBtn.MouseLeftButtonUp += (_, e) => { e.Handled = true; SetPinned(!_pinned); };
        _root.Children.Add(_pinBtn);
        UpdatePinVisual();
    }

    /// <summary>图钉的外观跟着固定状态走：灰色=会自己收起，橙色=已固定在桌面。</summary>
    private void UpdatePinVisual()
    {
        var ink = new SolidColorBrush(_pinned ? Color.FromRgb(0xE8, 0x96, 0x54) : Color.FromRgb(0x8A, 0x8A, 0x8A));
        _pinHead.Fill = ink;
        _pinBody.Fill = ink;
        _pinBtn.Background = new SolidColorBrush(_pinned
            ? Color.FromArgb(0xEC, 0xFF, 0xF3, 0xE4)
            : Color.FromArgb(0x96, 0xFF, 0xFF, 0xFF));
    }

    /// <summary>把「拖文件到绳上」接住：支持资源管理器的文件，也支持从网页拖来的位图。</summary>
    private void HookDrop()
    {
        AllowDrop = true;
        _root.AllowDrop = true;
        _root.DragEnter += (_, e) => OnDragOver(e);
        _root.DragOver += (_, e) => OnDragOver(e);
        _root.DragLeave += (_, _) => _dropHint.Visibility = Visibility.Collapsed;
        _root.Drop += OnDrop;
    }

    private void OnDragOver(DragEventArgs e)
    {
        bool ok = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap);
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        if (!ok) return;

        _dragGuard?.Stop();        // 真的在拖文件，取消自动收回
        if (!_shown) ShowLine();   // 拖到顶部时绳子可能还没落下，这里补一次
        _hideTimer.Stop();
        _dropHint.Visibility = Visibility.Visible;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        _dropHint.Visibility = Visibility.Collapsed;
        e.Handled = true;
        try
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                HangPaths(files);
            }
            else if (e.Data.GetData(DataFormats.Bitmap) is BitmapSource bmp)
            {
                HangPaths(new[] { SaveDroppedBitmap(bmp) });
            }
        }
        catch (Exception ex) { Diag.Log("拖放挂载失败：" + ex.Message); }
    }

    /// <summary>网页里拖来的位图没有源文件，先落盘再当普通图片挂。</summary>
    private static string SaveDroppedBitmap(BitmapSource bmp)
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clothesline", "Hang");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"drop_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using (var fs = File.Create(path)) enc.Save(fs);
        Diag.Log("拖入的位图已存为 " + path);
        return path;
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
        // 常驻模式：鼠标进绳区就取消穿透（好点图和图钉），离开绳区就恢复穿透。
        // 这样绳子一直挂在那儿，也不会挡住屏幕顶部本来要点的东西。
        if (_pinned)
        {
            bool inside = IsPointInStrip(x, y);
            if (inside != _stripInteractive)
            {
                _stripInteractive = inside;
                SetClickThrough(!inside);
            }
            return;
        }

        if (_shown) return;

        // 平时要真正触顶(y<=2)才唤出；但"按住左键往上传文件"这个动作很难正好压在 2px 里，
        // 所以拖拽状态下把判定放宽到 40px —— 拖到屏幕顶部附近绳子就滑下来接住。
        bool dragging = (Native.GetKeyState(Native.VK_LBUTTON) & 0x8000) != 0;
        int trigger = dragging ? 40 : 2;
        if (y > trigger) return;

        int w = Native.GetSystemMetrics(0);
        if (x < 0 || x > w) return; // 只管主屏
        if (IsForegroundFullscreen()) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_shown || _pinned) return;
            ShowLine();
            if (dragging) ArmDragIdleGuard(); // 万一只是在上边缘拖选文字，1.5 秒内没文件进来就收回
        });
    }

    /// <summary>因拖拽而露出的绳子，若迟迟没有文件拖进来（不是真的在拖文件），自动收回。</summary>
    private void ArmDragIdleGuard()
    {
        _dragGuard ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _dragGuard.Stop();
        _dragGuard.Tick -= OnDragGuardTick;
        _dragGuard.Tick += OnDragGuardTick;
        _dragGuard.Start();
    }

    private void OnDragGuardTick(object? sender, EventArgs e)
    {
        _dragGuard?.Stop();
        if (_dropHint.Visibility != Visibility.Visible && !_pinned && _shown) HideLine();
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

    public void TogglePin() => SetPinned(!_pinned);

    /// <summary>
    /// 把晾绳固定在桌面（true）/ 取消固定并收起（false）。
    /// 这个状态会写进设置，下次启动照旧——想要"永远挂着"的人不用每次重新按。
    /// </summary>
    public void SetPinned(bool on)
    {
        if (on == _pinned)
        {
            if (on && !_shown) { ShowLine(); _stripInteractive = true; SetClickThrough(false); }
            return;
        }

        _pinned = on;
        _settings.PinnedToDesktop = on;
        _settings.Save();
        _hideTimer.Stop();
        _peekTimer?.Stop();

        if (on)
        {
            ShowLine();
            _stripInteractive = true;
            SetClickThrough(false);
        }
        else
        {
            SetClickThrough(true); // 收起前先穿透，免得滑动途中还挡着下面
            HideLine();
        }

        UpdatePinVisual();
        _tray?.SetCount(_items.Count); // 托盘提示里的"（已固定）"跟着变
        Diag.Log(on ? "晾绳：已固定在桌面（下次启动仍是）" : "晾绳：取消固定，恢复自动收起");
    }

    /// <summary>启动时若上次是固定状态，直接挂出来。</summary>
    private void ApplyPinnedFromSettings()
    {
        if (!_settings.PinnedToDesktop) return;
        _pinned = true;
        _stripInteractive = true;
        ShowLine();
        SetClickThrough(false);
        UpdatePinVisual();
        Diag.Log("晾绳：按上次的设置，启动即固定在桌面");
    }

    /// <summary>屏幕坐标（物理像素）是否落在绳区里。</summary>
    private bool IsPointInStrip(int x, int y)
    {
        double scale = _hwnd != IntPtr.Zero ? Native.GetDpiForWindow(_hwnd) / 96.0 : 1.0;
        return x >= 0 && x <= Width * scale && y >= 0 && y <= StripH * scale;
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
        return IsPointInStrip(p.X, p.Y);
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
        if (!_settings.AutoHangClipboard) return;
        if (Environment.TickCount64 < _suppressClipboardUntil) return;
        try
        {
            // 复制文件（资源管理器 Ctrl+C）不算截图，交给拖放/右键菜单那条路
            if (Clipboard.ContainsFileDropList() || Clipboard.ContainsAudio()) return;
            if (!Clipboard.ContainsImage()) return;
            if (_exclClipFmt != 0 && Native.IsClipboardFormatAvailable(_exclClipFmt)) return; // 密码管理器标记的私有内容

            var raw = TryGetClipboardImage();   // 剪贴板正被别的程序占用时，重试几次
            if (raw == null || raw.PixelWidth < 8 || raw.PixelHeight < 8) return;
            var img = NormalizeAlpha(raw);
            if (_lastFileItem is { } last
                && (DateTime.Now - last.t).TotalSeconds < 5
                && last.w == img.PixelWidth && last.h == img.PixelHeight)
            {
                Diag.Log("剪贴板：与刚落盘的截图同尺寸，视为同一次截图，跳过");
                return; // Win+Shift+S 已按文件挂过，去重
            }

            var store = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clothesline", "Hang");
            Directory.CreateDirectory(store);
            var file = Path.Combine(store, $"clip_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using (var fs = File.Create(file)) enc.Save(fs);
            Diag.Log($"剪贴板：挂上 {img.PixelWidth}×{img.PixelHeight} 图片 → {Path.GetFileName(file)}");
            AddItem(file, false, img);
        }
        catch (Exception e) { Diag.Log("剪贴板读取失败：" + e.Message); }
    }

    /// <summary>微信/QQ 截图刚写完剪贴板时可能还被占用，重试几次再放弃。</summary>
    private static System.Windows.Media.Imaging.BitmapSource? TryGetClipboardImage()
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                var img = Clipboard.GetImage();
                if (img != null) return img;
            }
            catch { }
            System.Threading.Thread.Sleep(80);
        }
        return null;
    }

    /// <summary>
    /// CF_DIB 里 32 位位图的 alpha 字节常常是垃圾（多半全 0）。WPF 照单全收会把整张图当成全透明，
    /// 相框里就只剩一块灰。这里抽样看 alpha：几乎全 0 就视为"本来就没有透明"，转成 Bgr24。
    /// </summary>
    private static System.Windows.Media.Imaging.BitmapSource NormalizeAlpha(System.Windows.Media.Imaging.BitmapSource src)
    {
        if (src.Format != PixelFormats.Bgra32 && src.Format != PixelFormats.Pbgra32) return src;

        int w = src.PixelWidth, h = src.PixelHeight;
        if (w <= 0 || h <= 0) return src;

        int stride = w * 4;
        var row = new byte[stride];
        long total = 0, opaque = 0;
        int stepY = Math.Max(1, h / 40);
        for (int y = 0; y < h; y += stepY)
        {
            try { src.CopyPixels(new Int32Rect(0, y, w, 1), row, stride, 0); }
            catch { return src; }
            for (int x = 3; x < stride; x += 4)
            {
                total++;
                if (row[x] != 0) opaque++;
            }
        }
        if (total == 0 || opaque * 100 / total >= 5) return src;   // alpha 有效，别动

        var conv = new FormatConvertedBitmap(src, PixelFormats.Bgr24, null, 0);
        conv.Freeze();
        Diag.Log("剪贴板：alpha 通道为空，按无透明处理");
        return conv;
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
