// Clothesline — Windows 版「晾衣绳」
// 改编自 Tendedero (macOS) by Alejandro Buján — https://github.com/alejandrobujan/tendedero (MIT)
// 独立重写，不含原作代码。

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Clothesline;

/// <summary>首启引导：告诉第一次打开的人这东西在哪、怎么用。托盘菜单可随时重看。</summary>
internal sealed class OnboardingWindow : Window
{
    private static OnboardingWindow? _open;

    private static readonly Color Bg = Color.FromRgb(0xFB, 0xF7, 0xF0);
    private static readonly Color Ink = Color.FromRgb(0x2E, 0x2A, 0x24);
    private static readonly Color Muted = Color.FromRgb(0x7A, 0x6E, 0x5E);
    private static readonly Color Accent = Color.FromRgb(0xB0, 0x66, 0x30);
    private static readonly Color Line = Color.FromRgb(0xE8, 0xDC, 0xC8);

    private readonly LineWindow _line;
    private readonly Settings _settings;

    public static void ShowFor(LineWindow line, Settings settings)
    {
        if (_open != null)
        {
            _open.Activate();
            return;
        }
        _open = new OnboardingWindow(line, settings);
        _open.Closed += (_, _) => _open = null;
        _open.Show();
    }

    private OnboardingWindow(LineWindow line, Settings settings)
    {
        _line = line;
        _settings = settings;

        Title = "Clothesline — 使用说明";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");

        var card = new Border
        {
            Background = new SolidColorBrush(Bg),
            BorderBrush = new SolidColorBrush(Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(18),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            { BlurRadius = 28, ShadowDepth = 6, Direction = 270, Opacity = 0.22 },
        };
        card.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };

        var body = new StackPanel { Margin = new Thickness(34, 28, 34, 26) };

        // 关闭按钮
        var close = new TextBlock
        {
            Text = "\u2715",
            FontSize = 14,
            Foreground = new SolidColorBrush(Muted),
            HorizontalAlignment = HorizontalAlignment.Right,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, -12, -12, 0),
            Padding = new Thickness(6),
        };
        close.MouseLeftButtonUp += (_, _) => Close();
        body.Children.Add(close);

        body.Children.Add(new TextBlock
        {
            Text = "晾衣绳 · Clothesline",
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Ink),
            Margin = new Thickness(0, -14, 0, 4),
        });
        body.Children.Add(new TextBlock
        {
            Text = "截图随手一挂，桌面干干净净。它没有主窗口，平时就躲在右下角托盘里。",
            FontSize = 13,
            Foreground = new SolidColorBrush(Muted),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21,
            Margin = new Thickness(0, 0, 0, 20),
        });

        body.Children.Add(Step("1", "鼠标推到屏幕最上沿", "绳子就会滑下来。也可以按 Ctrl+Alt+T，或左键单击托盘里的晾衣夹图标固定。"));
        body.Children.Add(Step("2", "截图会自动挂上去", "用 Win+PrtScn 截屏、或用系统录屏，成品一落地就挂上绳子。想挂旧图：右键它选「挂到晾衣绳」，或托盘右键「挂张图片…」。"));
        body.Children.Add(Step("3", "单击复制 · 双击打开", "按住拖出去可发给微信、Word；鼠标停在卡片上会出现 \u2715，点了丢进回收站。"));

        // 试一试
        var tryRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 16) };
        tryRow.Children.Add(Pill("试一试：让绳子滑下来", Accent, Colors.White, () =>
        {
            _line.Peek(6);
            // 说明窗会挡住视线，缩到任务栏后面去，让用户看见绳子
            WindowState = WindowState.Minimized;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6.5) };
            t.Tick += (_, _) => { t.Stop(); WindowState = WindowState.Normal; Activate(); };
            t.Start();
        }));
        tryRow.Children.Add(Pill("打开截图文件夹", Color.FromRgb(0xEE, 0xE5, 0xD6), Ink, OpenShots));
        body.Children.Add(tryRow);

        // 两个开关
        body.Children.Add(Switch("开机自动启动（推荐）", _settings.RunAtStartup, v =>
        {
            _settings.RunAtStartup = v;
            _settings.Save();
            TrayIcon.SetStartup(v);
        }));
        body.Children.Add(Switch("在图片右键菜单里加「挂到晾衣绳」", _settings.ContextMenuEnabled, v =>
        {
            _settings.ContextMenuEnabled = v;
            _settings.Save();
            ShellMenu.Apply(v);
        }));

        body.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Line),
            Margin = new Thickness(0, 16, 0, 12),
        });

        var foot = new StackPanel();
        foot.Children.Add(new TextBlock
        {
            Text = "提示：Win11 的右键菜单默认精简，「挂到晾衣绳」在「显示更多选项」里（或按 Shift+F10）。随时可在托盘图标上点右键 →「使用说明」重看这一页。",
            FontSize = 12,
            Foreground = new SolidColorBrush(Muted),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10),
        });
        var start = Pill("开始使用", Color.FromRgb(0x3A, 0x33, 0x2A), Colors.White, Close);
        start.HorizontalAlignment = HorizontalAlignment.Right;
        start.Margin = new Thickness(0);
        foot.Children.Add(start);
        body.Children.Add(foot);

        card.Child = body;
        Content = card;
    }

    private static UIElement Step(string num, string title, string desc)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var dot = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Accent),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = num,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        row.Children.Add(dot);

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Ink),
            Margin = new Thickness(0, 1, 0, 3),
        });
        stack.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(Muted),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 20,
        });
        Grid.SetColumn(stack, 1);
        row.Children.Add(stack);
        return row;
    }

    private static UIElement Switch(string text, bool value, Action<bool> onChange)
    {
        var cb = new CheckBox
        {
            Content = text,
            IsChecked = value,
            FontSize = 13,
            Foreground = new SolidColorBrush(Ink),
            Margin = new Thickness(0, 2, 0, 8),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        cb.Checked += (_, _) => onChange(true);
        cb.Unchecked += (_, _) => onChange(false);
        return cb;
    }

    private static Border Pill(string text, Color bg, Color fg, Action onClick)
    {
        var b = new Border
        {
            Background = new SolidColorBrush(bg),
            CornerRadius = new CornerRadius(17),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(fg),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
            },
        };
        b.MouseEnter += (_, _) => b.Opacity = 0.85;
        b.MouseLeave += (_, _) => b.Opacity = 1;
        b.MouseLeftButtonUp += (_, _) => onClick();
        return b;
    }

    private static void OpenShots()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch (Exception e) { Diag.Log("打开截图文件夹失败 " + e.Message); }
    }
}
