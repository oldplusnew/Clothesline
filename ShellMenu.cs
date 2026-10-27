// Clothesline — Windows 版「晾衣绳」
// 改编自 Tendedero (macOS) by Alejandro Buján — https://github.com/alejandrobujan/tendedero (MIT)
// 独立重写，不含原作代码。

using System;
using System.IO;
using Microsoft.Win32;

namespace Clothesline;

/// <summary>
/// 在图片 / 视频文件的右键菜单里加一项「挂到晾衣绳」。
/// 只写 HKCU，无需管理员权限，删 exe 后可在托盘菜单一键移除。
/// Win11 的旧式动词会归到「显示更多选项」（Shift+F10）里，属系统行为。
/// </summary>
internal static class ShellMenu
{
    private const string Verb = "Clothesline.Hang";
    private static readonly string[] Kinds = { "image", "video" };

    public static void Apply(bool enable)
    {
        string exe = Environment.ProcessPath ?? "";
        if (enable && !File.Exists(exe))
        {
            Diag.Log("右键菜单：取不到 exe 路径，跳过");
            return;
        }

        foreach (string kind in Kinds)
        {
            string key = $@"Software\Classes\SystemFileAssociations\{kind}\shell\{Verb}";
            try
            {
                if (enable)
                {
                    using RegistryKey? k = Registry.CurrentUser.CreateSubKey(key, writable: true);
                    if (k == null) continue;
                    k.SetValue(null, "挂到晾衣绳");   // 默认值即菜单文字（老式动词）
                    k.SetValue("MUIVerb", "挂到晾衣绳");
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
                Diag.Log($"右键菜单：{kind} 失败 {e.Message}");
            }
        }
        Diag.Log(enable ? "右键菜单：已注册「挂到晾衣绳」" : "右键菜单：已移除");
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
