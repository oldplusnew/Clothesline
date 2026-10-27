using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace Clothesline;

/// <summary>挂在绳上的一张图：玻璃相框 + 晾衣夹 + 摆动。</summary>
internal sealed class HangItem
{
    public const double FrameW = 158, FrameH = 96, PinH = 30;

    public string FilePath { get; }
    public bool IsVideo { get; }

    private readonly Grid _root;
    private readonly RotateTransform _rot = new();
    private readonly Border _flash;
    private readonly ImageSource? _preview;

    private Point _down;
    private bool _pressed, _dragged, _doubleFired;

    public FrameworkElement Visual => _root;

    public event Action<HangItem>? CopyClick;
    public event Action<HangItem>? OpenClick;
    public event Action<HangItem>? UnhangClick; // 拖进文件夹：取下，文件保留
    public event Action<HangItem>? DeleteClick; // 点 ✕：删文件（回收站）

    public HangItem(string path, bool isVideo, ImageSource? image)
    {
        FilePath = path;
        IsVideo = isVideo;
        _preview = image;

        _root = new Grid
        {
            Width = FrameW,
            Background = Brushes.Transparent, // 必须可命中测试
            Cursor = Cursors.Hand,
            Margin = new Thickness(8, 0, 8, 0),
            RenderTransform = _rot,
        };
        _rot.CenterX = FrameW / 2;
        _rot.CenterY = 0;

        try
        {
            _root.ToolTip = $"{Path.GetFileName(path)}\n{File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}";
        }
        catch { }

        // 玻璃相框
        var frame = new Border
        {
            Width = FrameW,
            Height = FrameH,
            Margin = new Thickness(0, PinH - 6, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xE0, 0xFF, 0xFF, 0xFF)),
            Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(3),
            Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 5, Direction = 270, Opacity = 0.45 },
        };
        if (image != null)
        {
            frame.Child = new Image { Source = image, Stretch = Stretch.Uniform, Margin = new Thickness(3) };
        }
        else
        {
            var g = new Grid { Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x21, 0x26, 0x2D)) };
            g.Children.Add(new TextBlock
            {
                Text = "\u25B6",
                FontSize = 30,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            g.Children.Add(new TextBlock
            {
                Text = Path.GetFileName(path),
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0xB6, 0xBD, 0xC5)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 4, 6),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            frame.Child = g;
        }
        _root.Children.Add(frame);

        // 晾衣夹
        var pin = new Border
        {
            Width = 12,
            Height = PinH,
            CornerRadius = new CornerRadius(4, 4, 5, 5),
            Background = new LinearGradientBrush(Color.FromRgb(0xD8, 0xA1, 0x63), Color.FromRgb(0x9C, 0x69, 0x32), 90),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x77, 0x4A, 0x22)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 2, Direction = 270, Opacity = 0.35 },
        };
        _root.Children.Add(pin);
        _root.Children.Add(new Ellipse
        {
            Width = 5,
            Height = 5,
            Fill = new SolidColorBrush(Color.FromRgb(0x7A, 0x4F, 0x26)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 0, 0),
        });

        // 右上角删除按钮（悬停浮现）
        var x = new Button
        {
            Width = 20,
            Height = 20,
            Opacity = 0,
            Content = "\u2715",
            FontSize = 9,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, PinH - 14, 2, 0),
            Cursor = Cursors.Arrow,
        };
        var tmpl = new ControlTemplate(typeof(Button));
        var bf = new FrameworkElementFactory(typeof(Border));
        bf.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(0xCC, 0x20, 0x20, 0x20)));
        bf.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        bf.AppendChild(cp);
        tmpl.VisualTree = bf;
        x.Template = tmpl;
        x.BorderThickness = new Thickness(0);
        x.Click += (_, _) => DeleteClick?.Invoke(this);
        _root.Children.Add(x);

        // “已复制”提示
        _flash = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xC8, 0x2E, 0x7D, 0x32)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 8, 6),
            Opacity = 0,
            IsHitTestVisible = false,
            Child = new TextBlock { Text = "已复制", FontSize = 11, Foreground = Brushes.White },
        };
        _root.Children.Add(_flash);

        // 悬停浮现删除按钮
        _root.MouseEnter += (_, _) => x.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.95, TimeSpan.FromSeconds(0.12)));
        _root.MouseLeave += (_, _) => x.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(0.25)));

        // 交互：单击复制 / 双击打开 / 拖出分享
        _root.MouseLeftButtonDown += (_, e) =>
        {
            _down = e.GetPosition(null);
            _pressed = true;
            if (e.ClickCount == 2)
            {
                _doubleFired = true;
                OpenClick?.Invoke(this);
            }
        };
        _root.MouseMove += (_, e) =>
        {
            if (!_pressed || Mouse.LeftButton != MouseButtonState.Pressed) return;
            var d = e.GetPosition(null) - _down;
            if (Math.Abs(d.X) < 5 && Math.Abs(d.Y) < 5) return;
            _pressed = false;
            _dragged = true;
            StartDrag();
        };
        _root.MouseLeftButtonUp += (_, _) =>
        {
            if (!_pressed)
            {
                _doubleFired = false;
                return;
            }
            _pressed = false;
            if (_doubleFired) { _doubleFired = false; return; }
            if (_dragged) { _dragged = false; return; }
            if (IsVideo) OpenClick?.Invoke(this);
            else CopyClick?.Invoke(this);
        };
    }

    private void StartDrag()
    {
        try
        {
            var data = new DataObject();
            data.SetFileDropList(new System.Collections.Specialized.StringCollection { FilePath });
            if (!IsVideo)
            {
                try
                {
                    var full = BitmapFrame.Create(new Uri(FilePath), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    data.SetImage(full);
                }
                catch { }
            }
            var effect = DragDrop.DoDragDrop(_root, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
            if (effect != DragDropEffects.None && IsForegroundExplorer())
                UnhangClick?.Invoke(this); // 拖进资源管理器：取下（文件留在目标处）
        }
        catch { }
        finally { _dragged = false; }
    }

    private static bool IsForegroundExplorer()
    {
        var fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var sb = new StringBuilder(64);
        Native.GetClassName(fg, sb, 64);
        return sb.ToString() is "CabinetWClass" or "ExploreWClass";
    }

    public void Swing(double initialAngle)
    {
        _rot.Angle = initialAngle;
        var ease = new ElasticEase { Oscillations = 2, Springiness = 9, EasingMode = EasingMode.EaseOut };
        _rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, TimeSpan.FromSeconds(1.2)) { EasingFunction = ease });
    }

    public void Breeze()
    {
        var a = new DoubleAnimation(-2.2, 2.2, TimeSpan.FromSeconds(1.0))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(2),
            EasingFunction = new SineEase(),
        };
        _rot.BeginAnimation(RotateTransform.AngleProperty, a);
    }

    public void FlashCopied()
    {
        var a = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.2))
        {
            BeginTime = TimeSpan.FromSeconds(0.15),
            EasingFunction = new SineEase(),
        };
        _flash.BeginAnimation(UIElement.OpacityProperty, a);
    }
}
