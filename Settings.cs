using System;
using System.IO;
using System.Text.Json;

namespace Clothesline;

internal sealed class Settings
{
    public bool HangCopiedImages { get; set; } = false;
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
