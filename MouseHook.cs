using System;
using System.Runtime.InteropServices;

namespace Clothesline;

/// <summary>全局低级鼠标钩子：只关心鼠标位置（屏幕物理坐标），用于探测指针触顶。</summary>
internal sealed class MouseHook : IDisposable
{
    private IntPtr _hook;
    private readonly Native.LowLevelMouseProc _proc; // 持有委托防止被 GC 回收

    public event Action<int, int>? MouseMoved;

    public MouseHook()
    {
        _proc = HookProc;
    }

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)Native.WM_MOUSEMOVE)
        {
            try
            {
                var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                MouseMoved?.Invoke(info.pt.X, info.pt.Y);
            }
            catch { }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}
