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

    public TrayIcon(LineWindow win, Settings settings)
    {
        _win = win;
        _settings = settings;
        _hwnd = new WindowInteropHelper(win).Handle;

        _nid = new Native.NOTIFYICONDATA
        {
            hWnd = _hwnd,
            uID = 1,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
            uCallbackMessage = Native.WM_APP + 1,
            hIcon = CreateHIcon(),
            szTip = "Clothesline — 挂着 0 张",
        };
        _added = Native.Shell_NotifyIcon(Native.NIM_ADD, ref _nid);
    }

    public void SetCount(int n)
    {
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
        _ = Native.AppendMenu(menu, Native.MF_STRING | (_settings.HangCopiedImages ? Native.MF_CHECKED : 0), 102, "挂起复制的图像");
        _ = Native.AppendMenu(menu, Native.MF_STRING | (_settings.RunAtStartup ? Native.MF_CHECKED : 0), 103, "开机自动启动");
        _ = Native.AppendMenu(menu, Native.MF_SEPARATOR, 0, null);
        _ = Native.AppendMenu(menu, Native.MF_STRING, 104, "打开截图文件夹");
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
        }
    }

    private void ToggleStartup()
    {
        _settings.RunAtStartup = !_settings.RunAtStartup;
        _settings.Save();
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key != null)
            {
                if (_settings.RunAtStartup && Environment.ProcessPath is { } exe)
                    key.SetValue("Clothesline", $"\"{exe}\"");
                else
                    key.DeleteValue("Clothesline", false);
            }
        }
        catch { }
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
