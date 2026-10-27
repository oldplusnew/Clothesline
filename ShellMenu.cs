// Clothesline — Windows 版「晾衣绳」
// 改编自 Tendedero (macOS) by Alejandro Buján — https://github.com/alejandrobujan/tendedero (MIT)
// 独立重写，不含原作代码。

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace Clothesline;

/// <summary>
/// 在图片 / 视频的右键菜单里加一项「挂到晾衣绳」。
/// 只写 HKCU，无需管理员权限，删 exe 后可在托盘菜单一键移除。
/// Win11 的旧式动词会归到「显示更多选项」（Shift+F10）里，属系统行为。
/// </summary>
internal static class ShellMenu
{
    private const string Verb = "Clothesline.Hang";
    private const string Text = "挂到晾衣绳";

    // 感知类型（覆盖绝大多数图/视频），再按扩展名逐个补一遍——有些格式没有正确的 PerceivedType，
    // 或者被第三方看图软件改了默认关联，两套都写才稳。
    private static readonly string[] Kinds = { "image", "video" };

    private static readonly string[] Exts =
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".gif", ".bmp", ".dib",
        ".webp", ".heic", ".heif", ".tif", ".tiff", ".avif", ".ico", ".svg", ".jxl",
        ".mp4", ".mov", ".m4v", ".mkv", ".avi", ".wmv", ".webm", ".flv", ".mpg", ".mpeg",
    };

    public static void Apply(bool enable)
    {
        string exe = Environment.ProcessPath ?? "";
        if (enable && !File.Exists(exe))
        {
            Diag.Log("右键菜单：取不到 exe 路径，跳过");
            return;
        }

        foreach (string kind in Kinds)
            WriteKey($@"Software\Classes\SystemFileAssociations\{kind}\shell\{Verb}", enable, exe);

        foreach (string ext in Exts)
            WriteKey($@"Software\Classes\{ext}\shell\{Verb}", enable, exe);

        if (enable)
        {
            try { Native.SHChangeNotify(Native.SHCNE_ASSOCCHANGED, Native.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); }
            catch (Exception e) { Diag.Log("右键菜单：刷新外壳失败 " + e.Message); }
        }

        Diag.Log(enable
            ? $"右键菜单：已注册「{Text}」（{Kinds.Length} 类 + {Exts.Length} 种扩展名）"
            : "右键菜单：已移除");
    }

    private static void WriteKey(string key, bool enable, string exe)
    {
        try
        {
            if (enable)
            {
                using RegistryKey? k = Registry.CurrentUser.CreateSubKey(key, writable: true);
                if (k == null) return;
                k.SetValue(null, Text);          // 默认值即菜单文字（老式动词）
                k.SetValue("MUIVerb", Text);
                k.SetValue("Icon", exe);
                k.SetValue("MultiSelectModel", "Player");
                using RegistryKey? cmd = k.CreateSubKey("command", writable: true);
                cmd?.SetValue(null, $"\"{exe}\" --hang \"%1\"");
            }
            else
            {
                Registry.CurrentUser.DeleteSubKeyTree(key, false);
            }
        }
        catch (Exception e)
        {
            Diag.Log($"右键菜单：{key} 失败 {e.Message}");
        }
    }

    /// <summary>当前是否已注册（以 image 一项为准）。</summary>
    public static bool IsRegistered()
    {
        try
        {
            using RegistryKey? k = Registry.CurrentUser.OpenSubKey(
                $@"Software\Classes\SystemFileAssociations\image\shell\{Verb}\command");
            return k?.GetValue(null) is string s && s.Length > 0;
        }
        catch { return false; }
    }
}
