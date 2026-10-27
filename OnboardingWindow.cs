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

    /// <summary>打在可点击控件上的标记：从这里按下只算点击，不算拖动窗口。</summary>
    private const string ClickTag = "click";

    private Point _dragFromScreen;
    private double _dragFromLeft, _dragFromTop;
    private bool _dragArmed;

    public static void ShowFor(LineWindow line, Settings settings)
    {
        if (_open != null)
        {
            _open.Activate();
            return;
        }
        _open = new OnboardingWindow(line, settings);
        _open.Closed += (_, _) => _open = null;
        _open.Loaded += (_, _) => { _open?.Activate(); _open?.Focus(); };
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
        MaxHeight = SystemParameters.WorkArea.Height * 0.92;   // 小屏幕上超出就滚动，别顶出屏幕
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
        // 注意：这里绝不能用 Window.DragMove()。它内部发 WM_NCLBUTTONDOWN 进模态循环，
        // 松开鼠标的消息会被拖动逻辑吃掉，DragMove 返回时 MouseLeftButtonUp 已经丢了，
        // 于是所有靠 "抬起" 触发的控件（✕、按钮）全都点不动。
        // 改为自己按位移拖动窗口，不阻塞消息循环；按钮上按下则完全不拖。
        card.MouseLeftButtonDown += OnCardMouseDown;
        card.MouseMove += OnCardMouseMove;
        card.MouseLeftButtonUp += (_, _) => _dragArmed = false;

        var body = new StackPanel { Margin = new Thickness(34, 28, 34, 26) };

        // 关闭按钮（命中区放大一圈，别让人点不准）
        var close = new TextBlock
        {
            Text = "\u2715",
            FontSize = 14,
            Foreground = new SolidColorBrush(Muted),
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Center,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, -14, -14, 0),
            Padding = new Thickness(10, 7, 10, 7),
            Tag = ClickTag,
        };
        close.MouseEnter += (_, _) => close.Foreground = new SolidColorBrush(Ink);
        close.MouseLeave += (_, _) => close.Foreground = new SolidColorBrush(Muted);
        close.MouseLeftButtonUp += (_, e) => { e.Handled = true; Diag.Log("说明窗：点击关闭"); Close(); };
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
            Margin = new Thickness(0, 0, 0, 6),
        });
        body.Children.Add(new TextBlock
        {
            Text = "作者 oldplusnew　·　改编自 Tenedero（macOS）by Alejandro Buján",
            FontSize = 11.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0x8E, 0x7C)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 18),
        });

        body.Children.Add(Step("1", "鼠标推到屏幕最上沿", "绳子就会滑下来。也可以按 Ctrl+Alt+T，或左键单击托盘里的晾衣夹图标固定。"));
        body.Children.Add(Step("2", "截完图就挂上来了", "微信 Alt+A、QQ Ctrl+Alt+A、Win+Shift+S、Win+PrtScn 都行。微信截图只进剪贴板、不存盘，我们从剪贴板直接接住它，约 1 秒内挂上。"));
        body.Children.Add(Step("3", "单击复制 · 双击打开", "按住拖出去可发给微信、Word；鼠标停在卡片上会出现 \u2715，点了丢进回收站。"));
        body.Children.Add(Step("4", "想挂一张旧图片", "把它拖到屏幕最上沿松手；或在它上面点右键选「挂到晾衣绳」；或托盘右键「挂张图片…」挑文件。"));

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

        // 三个开关
        body.Children.Add(Switch("截图 / 复制的图片自动挂上（含微信 Alt+A）", _settings.AutoHangClipboard, v =>
        {
            _settings.AutoHangClipboard = v;
            _settings.Save();
        }));
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
        var start = Pill("开始使用", Color.FromRgb(0x3A, 0x33, 0x2A), Colors.White, () =>
        {
            Diag.Log("说明窗：点击开始使用");
            Close();
        });
        start.HorizontalAlignment = HorizontalAlignment.Right;
        start.Margin = new Thickness(0);
        foot.Children.Add(start);
        body.Children.Add(foot);

        card.Child = new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Content = card;

        // Esc / Enter 都能关（不用抬手找那枚 ✕）
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape || e.Key == Key.Enter) { e.Handled = true; Close(); }
        };
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
            Tag = ClickTag,
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
            Tag = ClickTag,
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
        b.MouseLeftButtonUp += (_, e) => { e.Handled = true; onClick(); };
        return b;
    }

    // ---- 自实现的窗口拖动（不用 DragMove，见构造函数里的说明） ----

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        _dragArmed = false;
        if (IsClickable(e.OriginalSource as DependencyObject, sender as DependencyObject)) return;
        _dragFromScreen = PointToScreen(e.GetPosition(this));
        _dragFromLeft = Left;
        _dragFromTop = Top;
        _dragArmed = true;
    }

    private void OnCardMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragArmed) return;
        if (e.LeftButton != MouseButtonState.Pressed) { _dragArmed = false; return; }

        // PointToScreen 得到的是光标的真实屏幕坐标，窗口挪动后这个值仍然稳定。
        Point now = PointToScreen(e.GetPosition(this));
        double dx = now.X - _dragFromScreen.X;
        double dy = now.Y - _dragFromScreen.Y;
        if (Math.Abs(dx) < 4 && Math.Abs(dy) < 4) return;   // 微抖不算拖

        Left = _dragFromLeft + dx;
        Top = _dragFromTop + dy;
    }

    /// <summary>从原始命中点往上走，判断这一下是按在按钮/复选框/可点击块上。</summary>
    private static bool IsClickable(DependencyObject? src, DependencyObject? stopAt)
    {
        while (src != null)
        {
            if (ReferenceEquals(src, stopAt)) return false;
            if (src is System.Windows.Controls.Primitives.ButtonBase) return true;
            if (src is FrameworkElement fe && fe.Tag is string s && s == ClickTag) return true;
            src = src is Visual || src is System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(src)
                : LogicalTreeHelper.GetParent(src);
        }
        return false;
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
