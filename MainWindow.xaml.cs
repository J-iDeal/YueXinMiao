using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace 桌面萌宠
{
    public partial class MainWindow : Window
    {
        private const int DefaultFrameDelayMs = 100;

        private readonly List<BitmapSource> gifFrames = new();
        private readonly List<int> frameDelays = new();

        private int currentFrame = 0;
        private DispatcherTimer? timer;

        private double originalGifWidth;
        private double originalGifHeight;
        private double currentScale = 1.0;

        private Popup? currentMenuPopup;
        private Popup? currentSubMenuPopup;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;   // 不在 Alt+Tab 中显示
            exStyle &= ~WS_EX_APPWINDOW;    // 移除强制显示标记
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        public MainWindow()
        {
            InitializeComponent();

            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 60;

            LoadGif("pet.gif");
        }

        private void LoadGif(string filePath)
        {
            try
            {
                var decoder = new GifBitmapDecoder(
                    new Uri(filePath, UriKind.Relative),
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                gifFrames.Clear();
                frameDelays.Clear();

                foreach (var frame in decoder.Frames)
                {
                    var frozen = BitmapFrame.Create(frame);
                    frozen.Freeze();
                    gifFrames.Add(frozen);

                    frameDelays.Add(ExtractFrameDelay(frame));
                }

                if (gifFrames.Count > 0)
                {
                    PetImage.Source = gifFrames[0];
                    currentFrame = 0;

                    originalGifWidth = gifFrames[0].PixelWidth;
                    originalGifHeight = gifFrames[0].PixelHeight;

                    ApplyScale(1.0);

                    StartAnimation();
                }
            }
            catch (Exception ex)
            {
                PetImage.Source = null;
                PetImage.Visibility = Visibility.Collapsed;
                MessageBox.Show(
                    $"GIF 加载失败: {ex.Message}\n请检查 pet.gif 文件是否存在。\n应用将退出。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private static int ExtractFrameDelay(BitmapFrame frame)
        {
            try
            {
                if (frame.Metadata is BitmapMetadata metadata
                    && metadata.ContainsQuery("/grctlext/Delay"))
                {
                    var raw = metadata.GetQuery("/grctlext/Delay");
                    if (raw is ushort centiseconds)
                    {
                        int ms = centiseconds * 10;
                        return ms > 0 ? ms : DefaultFrameDelayMs;
                    }
                }
            }
            catch
            {
            }
            return DefaultFrameDelayMs;
        }

        private void StartAnimation()
        {
            timer?.Stop();
            timer = null;

            timer = new DispatcherTimer();
            timer.Tick += OnFrameTick;

            timer.Interval = frameDelays.Count > 0
                ? TimeSpan.FromMilliseconds(frameDelays[0])
                : TimeSpan.FromMilliseconds(DefaultFrameDelayMs);

            timer.Start();
        }

        private void OnFrameTick(object? sender, EventArgs e)
        {
            if (gifFrames.Count == 0) return;

            currentFrame = (currentFrame + 1) % gifFrames.Count;
            PetImage.Source = gifFrames[currentFrame];

            if (frameDelays.Count > currentFrame && timer != null)
                timer.Interval = TimeSpan.FromMilliseconds(frameDelays[currentFrame]);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void ApplyScale(double scale)
        {
            currentScale = scale;

            var transform = new ScaleTransform(scale, scale);
            PetImage.LayoutTransform = transform;

            this.Width = originalGifWidth * scale;
            this.Height = originalGifHeight * scale;
        }

        private void SetSize(double scale)
        {
            double centerX = this.Left + this.Width / 2;
            double centerY = this.Top + this.Height / 2;

            ApplyScale(scale);

            this.Left = centerX - this.Width / 2;
            this.Top = centerY - this.Height / 2;
        }

        private void ResetPosition()
        {
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 60;
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            CloseAllPopups();

            currentMenuPopup = BuildMenuPopup();
            currentMenuPopup.IsOpen = true;
        }

        private Popup BuildMenuPopup()
        {
            var panel = new StackPanel();

            // --- 大小切换（带悬停子菜单） ---
            var sizeItem = CreateMenuRow("🔧  大小切换  ▶", null, null);
            panel.Children.Add(sizeItem);

            // --- 重置位置 ---
            var resetItem = CreateMenuRow("📍  重置位置", null, () =>
            {
                ResetPosition();
                CloseAllPopups();
            });
            panel.Children.Add(resetItem);

            // --- 分隔线 ---
            panel.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE)),
                Margin = new Thickness(12, 3, 12, 3)
            });

            // --- 退出 ---
            var exitItem = CreateMenuRow("❌  退出", null, () =>
            {
                CloseAllPopups();
                Application.Current.Shutdown();
            });
            panel.Children.Add(exitItem);

            var popup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = false,
                Placement = PlacementMode.MousePoint,
                Child = BuildMenuBorder(panel)
            };

            popup.Closed += (_, _) => CloseAllPopups();

            // 为「大小切换」绑定悬停事件（popup 创建后 sizeItem 才加入视觉树）
            sizeItem.MouseEnter += (_, _) =>
            {
                CloseSubPopup();
                currentSubMenuPopup = BuildSizeSubMenu(sizeItem);
                currentSubMenuPopup.IsOpen = true;
            };

            return popup;
        }

        private static Border BuildMenuBorder(UIElement content)
        {
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(4),
                Margin = new Thickness(4),
                Child = content,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromArgb(0x40, 0x00, 0x00, 0x00),
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Direction = 270,
                    Opacity = 1
                }
            };
        }

        private Popup BuildSizeSubMenu(FrameworkElement placementTarget)
        {
            var panel = new StackPanel();

            var sizes = new (string Label, double Scale)[]
            {
                ("小 (50%)",  0.5),
                ("中 (75%)",  0.75),
                ("原始(100%)", 1.0),
                ("大 (150%)", 1.5),
            };

            foreach (var (label, scale) in sizes)
            {
                double captured = scale;
                bool active = Math.Abs(currentScale - captured) < 0.01;
                var item = CreateMenuRow(label, active ? "●" : "", () =>
                {
                    SetSize(captured);
                    CloseAllPopups();
                });
                panel.Children.Add(item);
            }

            return new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                PlacementTarget = placementTarget,
                Placement = PlacementMode.Right,
                Child = BuildMenuBorder(panel)
            };
        }

        private static Border CreateMenuRow(string text, string? indicator, Action? onClick)
        {
            var row = new Border
            {
                Padding = new Thickness(12, 7, 20, 7),
                CornerRadius = new CornerRadius(5),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Child = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(16) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                }
            };

            var grid = (Grid)row.Child;

            if (!string.IsNullOrEmpty(indicator))
            {
                var dot = new TextBlock
                {
                    Text = indicator,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 0);
                grid.Children.Add(dot);
            }

            var label = new TextBlock
            {
                Text = text,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            var highlight = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            row.MouseEnter += (_, _) => row.Background = highlight;
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

            if (onClick != null)
            {
                row.MouseLeftButtonDown += (_, _) => onClick();
            }

            return row;
        }

        private void CloseAllPopups()
        {
            CloseSubPopup();
            if (currentMenuPopup != null)
            {
                currentMenuPopup.IsOpen = false;
                currentMenuPopup = null;
            }
        }

        private void CloseSubPopup()
        {
            if (currentSubMenuPopup != null)
            {
                currentSubMenuPopup.IsOpen = false;
                currentSubMenuPopup = null;
            }
        }
    }
}
