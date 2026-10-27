using System;
using System.IO;
using System.Text.Json;

namespace Clothesline;

internal sealed class Settings
{
    /// <summary>
    /// 剪贴板里的图片自动挂上（微信 Alt+A、QQ 截图、Win+Shift+S、网页复制图片等）。
    /// 微信/QQ 的截图只进剪贴板、不落盘，所以必须靠这一路才挂得上。默认开。
    /// </summary>
    public bool AutoHangClipboard { get; set; } = true;

    public bool RunAtStartup { get; set; } = false;

    /// <summary>是否已经看过首启引导（看过就不再自动弹出）。</summary>
    public bool FirstRunDone { get; set; } = false;

    /// <summary>是否在图片/视频的右键菜单里注册「挂到晾衣绳」。</summary>
    public bool ContextMenuEnabled { get; set; } = true;

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clothesline");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
                if (s != null) return s;
            }
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
