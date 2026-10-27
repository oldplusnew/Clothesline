// Clothesline — Windows 版「晾衣绳」
// 改编自 Tendedero (macOS) by Alejandro Buján — https://github.com/alejandrobujan/tendedero (MIT)
// 独立重写，不含原作代码。

using System;
using System.IO;

namespace Clothesline;

/// <summary>极简日志：写 %APPDATA%\Clothesline\log.txt，出错不抛。</summary>
internal static class Diag
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clothesline", "log.txt");

    public static void Log(string msg)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\r\n");
        }
        catch { }
    }
}
