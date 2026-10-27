using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Clothesline;

/// <summary>托盘图标（代码绘制 HICON）+ 右键菜单。</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly LineWindow _win;
    private readonly Settings _settings;
    private readonly IntPtr _hwnd;
    private Native.NOTIFYICONDATA _nid;
    private bool _added;
    private int _count;

    public TrayIcon(LineWindow win, Settings settings)
    {
        _win = win;
        _settings = settings;
        _hwnd = new WindowInteropHelper(win).Handle;

        _nid = new Native.NOTIFYICONDATA
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
            uCallbackMessage = Native.WM_APP + 1,
            hIcon = CreateHIcon(),
            szTip = "Clothesline — 挂着 0 张",
        };
        Diag.Log($"托盘：cbSize={_nid.cbSize} 结构大小={System.Runtime.InteropServices.Marshal.SizeOf<Native.NOTIFYICONDATA>()} hIcon=0x{_nid.hIcon.ToInt64():X} hwnd=0x{_hwnd.ToInt64():X}");
        Add();
        SchedulePromote();
    }

    /// <summary>延后 3 秒把图标标记为"常驻显示"——资源管理器是异步记账的，立刻查会查不到。</summary>
    private void SchedulePromote()
    {
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (_, _) => { t.Stop(); PromoteInTray(); };
        t.Start();
    }

    /// <summary>
    /// Win11 默认把新托盘图标塞进「^」折叠区。若本进程设置了关机自启，图标被折叠就永远看不见，
    /// 故主动把 HKCU\Control Panel\NotifyIconSettings 下本 exe 的条目标为 IsPromoted=1。
    /// </summary>
    private static void PromoteInTray()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            using var root = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Control Panel\NotifyIconSettings", writable: true);
            if (root == null) return;

            foreach (var name in root.GetSubKeyNames())
            {
                using var sub = root.OpenSubKey(name, writable: true);
                if (sub?.GetValue("ExecutablePath") is not string path) continue;
                if (!string.Equals(path, exe, StringComparison.OrdinalIgnoreCase)) continue;

                if (sub.GetValue("IsPromoted") is int v && v == 1) return; // 已经是常驻
                sub.SetValue("IsPromoted", 1, Microsoft.Win32.RegistryValueKind.DWord);
                Diag.Log("托盘：已标记为常驻显示（IsPromoted=1）");
                return;
            }
            Diag.Log("托盘：系统尚未记账，跳过常驻设置");
        }
        catch (Exception e) { Diag.Log("托盘：设置常驻失败 " + e.Message); }
    }

    /// <summary>注册托盘图标，返回是否成功（失败会记日志，绝不静默）。</summary>
    private bool Add()
    {
        if (_nid.hIcon == IntPtr.Zero) _nid.hIcon = CreateHIcon(); // 兜底重建

        _nid.uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP;
        _added = Native.Shell_NotifyIcon(Native.NIM_ADD, ref _nid);
        int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        Diag.Log(_added ? "托盘：注册成功" : $"托盘：注册失败 err={err}（图标将不可见）");
        return _added;
    }

    /// <summary>资源管理器重启（TaskbarCreated 广播）后重新挂上图标。</summary>
    public void ReAdd()
    {
        _added = false;
        Add();
        if (_added) SetCount(_count);
        Diag.Log("托盘：收到 TaskbarCreated，已重新注册");
        SchedulePromote();
    }

    public void SetCount(int n)
    {
        _count = n;
        if (!_added) return;
        _nid.uFlags = Native.NIF_TIP;
        _nid.szTip = $"Clothesline — 挂着 {n} 张";
        _ = Native.Shell_NotifyIcon(Native.NIM_MODIFY, ref _nid);
    }

    public void HandleTrayMessage(int msg)
    {
        if (msg == Native.WM_LBUTTONUP) _win.TogglePin(); // 左键：放下/收起
        else if (msg == Native.WM_RBUTTONUP) ShowMenu();
    }

    private void ShowMenu()
    {
        IntPtr menu = Native.CreatePopupMenu();
        _ = Native.AppendMenu(menu, Native.MF_STRING, 101, (_win.IsPinned ? "收起晾绳" : "放下晾绳") + "\tCtrl+Alt+T");
        _ = Native.AppendMenu(menu, Native.MF_SEPARATOR, 0, null);
        _ = Native.AppendMenu(menu, Native.MF_STRING | (_settings.ContextMenuEnabled ? Native.MF_CHECKED : 0), 106, "图片右键菜单：挂到晾衣绳");
        _ = Native.AppendMenu(menu, Native.MF_STRING | (_settings.HangCopiedImages ? Native.MF_CHECKED : 0), 102, "挂起复制的图像");
        _ = Native.AppendMenu(menu, Native.MF_STRING | (_settings.RunAtStartup ? Native.MF_CHECKED : 0), 103, "开机自动启动");
        _ = Native.AppendMenu(menu, Native.MF_SEPARATOR, 0, null);
        _ = Native.AppendMenu(menu, Native.MF_STRING, 104, "打开截图文件夹");
        _ = Native.AppendMenu(menu, Native.MF_STRING, 107, "使用说明…");
        _ = Native.AppendMenu(menu, Native.MF_SEPARATOR, 0, null);
        _ = Native.AppendMenu(menu, Native.MF_STRING, 105, "退出");

        _ = Native.SetForegroundWindow(_hwnd);
        _ = Native.GetCursorPos(out var pt);
        int cmd = Native.TrackPopupMenuEx(menu, Native.TPM_RETURNCMD | Native.TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
        _ = Native.DestroyMenu(menu);
        _ = Native.PostMessage(_hwnd, Native.WM_NULL, IntPtr.Zero, IntPtr.Zero); // KB135748 菜单失焦补丁

        switch (cmd)
        {
            case 101: _win.TogglePin(); break;
            case 102:
                _settings.HangCopiedImages = !_settings.HangCopiedImages;
                _settings.Save();
                break;
            case 103: ToggleStartup(); break;
            case 104: OpenScreenshotsFolder(); break;
            case 105: _win.RequestExit(); break;
            case 106:
                _settings.ContextMenuEnabled = !_settings.ContextMenuEnabled;
                _settings.Save();
                ShellMenu.Apply(_settings.ContextMenuEnabled);
                break;
            case 107: OnboardingWindow.ShowFor(_win, _settings); break;
        }
    }

    private void ToggleStartup()
    {
        _settings.RunAtStartup = !_settings.RunAtStartup;
        _settings.Save();
        SetStartup(_settings.RunAtStartup);
    }

    /// <summary>写/删 HKCU 的 Run 项，无需管理员权限。</summary>
    public static void SetStartup(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue("Clothesline", $"\"{exe}\"");
            else
                key.DeleteValue("Clothesline", false);
        }
        catch (Exception e) { Diag.Log("开机自启设置失败 " + e.Message); }
    }

    private static void OpenScreenshotsFolder()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
            Directory.CreateDirectory(dir);
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch { }
    }

    // 代码画托盘图标：一条绳 + 一个晾衣夹
    private static IntPtr CreateHIcon()
    {
        const int S = 32;
        var dv = new System.Windows.Media.DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var rope = new Pen(Brushes.White, 2.6);
            rope.Freeze();
            dc.DrawLine(rope, new Point(1, 10), new Point(31, 10));
            var pin = new LinearGradientBrush(Color.FromRgb(0xE8, 0x96, 0x54), Color.FromRgb(0xB0, 0x66, 0x30), 90);
            dc.DrawRectangle(pin, new Pen(Brushes.White, 0.6), new Rect(11, 7, 10, 18));
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(9.5, 3, 13, 8), 3.5, 3.5);
        }
        var rtb = new RenderTargetBitmap(S, S, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);

        var premul = new byte[S * S * 4];
        rtb.CopyPixels(premul, S * 4, 0);
        var straight = new byte[S * S * 4]; // Pbgra → 直通 alpha
        for (int i = 0; i < S * S; i++)
        {
            byte a = premul[i * 4 + 3];
            if (a == 0) continue;
            straight[i * 4 + 3] = a;
            straight[i * 4 + 0] = (byte)Math.Min(255, premul[i * 4] * 255 / a);
            straight[i * 4 + 1] = (byte)Math.Min(255, premul[i * 4 + 1] * 255 / a);
            straight[i * 4 + 2] = (byte)Math.Min(255, premul[i * 4 + 2] * 255 / a);
        }

        IntPtr hColor = Native.CreateBitmap(S, S, 1, 32, straight);
        IntPtr hMask = Native.CreateBitmap(S, S, 1, 1, new byte[S * 4]); // 32 行掩码（每行 4 字节）
        var info = new Native.ICONINFO { fIcon = true, xHotspot = 0, yHotspot = 0, hbmMask = hMask, hbmColor = hColor };
        IntPtr hIcon = Native.CreateIconIndirect(ref info);
        if (hColor != IntPtr.Zero) _ = Native.DeleteObject(hColor);
        if (hMask != IntPtr.Zero) _ = Native.DeleteObject(hMask);
        return hIcon;
    }

    public void Dispose()
    {
        if (_added)
        {
            _ = Native.Shell_NotifyIcon(Native.NIM_DELETE, ref _nid);
            _added = false;
        }
        if (_nid.hIcon != IntPtr.Zero)
        {
            _ = Native.DestroyIcon(_nid.hIcon);
            _nid.hIcon = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}
