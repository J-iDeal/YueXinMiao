using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace 月薪喵
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

        private bool isExiting;
        private bool hasCustomImage;
        private bool isBgmOn;

        private System.Windows.Forms.NotifyIcon? trayIcon;
        private MediaPlayer? bgmPlayer;

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

            SetupTrayIcon();

            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
            this.Top = SystemParameters.PrimaryScreenHeight - this.Height - 60;

            LoadGif();
            SetupBgm();
        }

        private void SetupTrayIcon()
        {
            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Text = "月薪喵",
                Visible = true
            };

            try
            {
                using var stream = GetEmbeddedStream("pet.gif");
                if (stream != null)
                {
                    using var gifImage = System.Drawing.Image.FromStream(stream);
                    var iconBitmap = new System.Drawing.Bitmap(gifImage, new System.Drawing.Size(32, 32));
                    var iconHandle = iconBitmap.GetHicon();
                    trayIcon.Icon = System.Drawing.Icon.FromHandle(iconHandle);
                }
            }
            catch
            {
                trayIcon.Icon = System.Drawing.SystemIcons.Application;
            }

            // 托盘右键菜单
            var trayMenu = new System.Windows.Forms.ContextMenuStrip();

            var showItem = trayMenu.Items.Add("显示/隐藏");
            showItem.Click += (_, _) => ToggleVisibility();

            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            var exitItem = trayMenu.Items.Add("退出");
            exitItem.Click += (_, _) =>
            {
                isExiting = true;
                trayIcon.Visible = false;
                trayIcon.Dispose();
                Application.Current.Shutdown();
            };

            trayIcon.ContextMenuStrip = trayMenu;

            // 左键单击 = 显示/隐藏
            trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    ToggleVisibility();
            };
        }

        private void ToggleVisibility()
        {
            if (this.Visibility == Visibility.Visible)
                this.Hide();
            else
                this.Show();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!isExiting)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                trayIcon?.Dispose();
            }
            base.OnClosing(e);
        }

        private void LoadGif()
        {
            try
            {
                var stream = GetEmbeddedStream("pet.gif");
                if (stream == null)
                {
                    MessageBox.Show("找不到内嵌的 pet.gif 资源。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                var decoder = new GifBitmapDecoder(
                    stream,
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
            panel.Children.Add(BuildMenuPadding(6));

            // --- 大小切换 ---
            var sizeItem = BuildSubMenuItem("大小切换");
            panel.Children.Add(sizeItem);

            // --- 图片管理 ---
            var imageItem = BuildSubMenuItem("图片管理");
            panel.Children.Add(imageItem);

            // --- 重置位置 ---
            panel.Children.Add(CreateMenuRow("重置位置", null, () =>
            {
                ResetPosition();
                CloseAllPopups();
            }));

            // --- 声音 ---
            panel.Children.Add(CreateMenuRow(
                isBgmOn ? "关闭声音" : "打开声音",
                null,
                () =>
                {
                    ToggleBgm();
                    CloseAllPopups();
                }));

            // --- 分隔线 ---
            panel.Children.Add(BuildSeparator());

            // --- 退出 ---
            panel.Children.Add(CreateMenuRow("退出", null, () =>
            {
                CloseAllPopups();
                Application.Current.Shutdown();
            }));

            panel.Children.Add(BuildMenuPadding(6));

            var popup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = false,
                Placement = PlacementMode.MousePoint,
                Child = BuildMenuBorder(panel)
            };

            popup.Closed += (_, _) => CloseAllPopups();

            sizeItem.MouseEnter += (_, _) =>
            {
                CloseSubPopup();
                currentSubMenuPopup = BuildSizeSubMenu(sizeItem);
                currentSubMenuPopup.IsOpen = true;
            };

            imageItem.MouseEnter += (_, _) =>
            {
                CloseSubPopup();
                currentSubMenuPopup = BuildImageSubMenu(imageItem);
                currentSubMenuPopup.IsOpen = true;
            };

            return popup;
        }

        private static Border BuildMenuBorder(UIElement content)
        {
            return new Border
            {
                Margin = new Thickness(28),
                Child = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0xFC, 0xFC, 0xFA)),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(6, 4, 6, 4),
                    Child = content,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Color.FromArgb(0x18, 0x00, 0x00, 0x00),
                        BlurRadius = 30,
                        ShadowDepth = 4,
                        Direction = 270,
                        Opacity = 1
                    }
                }
            };
        }

        private static Border BuildSubMenuItem(string text)
        {
            var row = new Border
            {
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Child = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = new GridLength(10) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                }
            };

            var grid = (Grid)row.Child;

            var label = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = 14,
                LineStackingStrategy = System.Windows.LineStackingStrategy.BlockLineHeight
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            // macOS 风格 chevron
            var chevron = new TextBlock
            {
                Text = "›",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = 14,
                LineStackingStrategy = System.Windows.LineStackingStrategy.BlockLineHeight
            };
            Grid.SetColumn(chevron, 2);
            grid.Children.Add(chevron);

            var highlight = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xEE));
            row.MouseEnter += (_, _) => row.Background = highlight;
            row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;

            return row;
        }

        private static FrameworkElement BuildMenuPadding(double height)
        {
            return new Border { Height = height };
        }

        private static FrameworkElement BuildSeparator()
        {
            return new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEB)),
                Margin = new Thickness(16, 5, 16, 5)
            };
        }

        private Popup BuildSizeSubMenu(FrameworkElement placementTarget)
        {
            var panel = new StackPanel();
            panel.Children.Add(BuildMenuPadding(6));

            var sizes = new (string Label, double Scale)[]
            {
                ("迷你 (25%)",  0.25),
                ("小号 (50%)",  0.5),
                ("中号 (75%)",  0.75),
                ("默认 (100%)", 1.0),
                ("大号 (150%)", 1.5),
                ("超大 (200%)", 2.0),
                ("巨大 (500%)", 5.0),
            };

            foreach (var (label, scale) in sizes)
            {
                double captured = scale;
                bool active = Math.Abs(currentScale - captured) < 0.01;
                var item = CreateMenuRow(label, active ? "checked" : null, () =>
                {
                    SetSize(captured);
                    CloseAllPopups();
                });
                panel.Children.Add(item);
            }

            panel.Children.Add(BuildMenuPadding(6));

            return new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                PlacementTarget = placementTarget,
                Placement = PlacementMode.Right,
                Child = BuildMenuBorder(panel)
            };
        }

        private Popup BuildImageSubMenu(FrameworkElement placementTarget)
        {
            var panel = new StackPanel();
            panel.Children.Add(BuildMenuPadding(6));

            panel.Children.Add(CreateMenuRow("更换图片", null, () =>
            {
                ChangeImage();
                CloseAllPopups();
            }));

            if (hasCustomImage)
            {
                panel.Children.Add(CreateMenuRow("恢复默认", null, () =>
                {
                    RestoreDefaultImage();
                    CloseAllPopups();
                }));
            }

            panel.Children.Add(BuildMenuPadding(6));

            return new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                PlacementTarget = placementTarget,
                Placement = PlacementMode.Right,
                Child = BuildMenuBorder(panel)
            };
        }

        private void ChangeImage()
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择宠物的GIF图片",
                Filter = "GIF图片|*.gif|PNG图片|*.png|JPG图片|*.jpg;*.jpeg|所有图片|*.gif;*.png;*.jpg;*.jpeg",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    LoadGifFromFile(dlg.FileName);
                    hasCustomImage = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"图片加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RestoreDefaultImage()
        {
            try
            {
                LoadGif();
                hasCustomImage = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"恢复默认图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadGifFromFile(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var decoder = new GifBitmapDecoder(
                stream,
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

                ApplyScale(currentScale);

                StartAnimation();
            }
        }

        private static Border CreateMenuRow(string text, string? indicator, Action? onClick)
        {
            bool hasIndicator = indicator != null;
            int labelColumn = hasIndicator ? 1 : 0;

            var grid = new Grid();
            if (hasIndicator)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            }
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var row = new Border
            {
                Padding = new Thickness(14, 10, 14, 10),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                Child = grid
            };

            if (indicator == "checked")
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(dot, 0);
                grid.Children.Add(dot);
            }

            var label = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = 14,
                LineStackingStrategy = System.Windows.LineStackingStrategy.BlockLineHeight,
                HorizontalAlignment = hasIndicator ? HorizontalAlignment.Left : HorizontalAlignment.Center
            };
            Grid.SetColumn(label, labelColumn);
            grid.Children.Add(label);

            var highlight = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xEE));
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

        private void SetupBgm()
        {
            try
            {
                var stream = GetEmbeddedStream("月薪喵.mp3");
                if (stream == null) return;

                var tmpPath = Path.Combine(Path.GetTempPath(), "月薪喵_bgm.mp3");
                if (!File.Exists(tmpPath))
                {
                    using (stream)
                    using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fs);
                    }
                }
                else
                {
                    stream.Dispose();
                }

                bgmPlayer = new MediaPlayer();
                bgmPlayer.Open(new Uri(tmpPath));
                bgmPlayer.MediaEnded += (_, _) =>
                {
                    if (isBgmOn && bgmPlayer != null)
                    {
                        bgmPlayer.Position = TimeSpan.Zero;
                        bgmPlayer.Play();
                    }
                };
            }
            catch
            {
                // BGM 加载失败，静默忽略
            }
        }

        private void ToggleBgm()
        {
            if (bgmPlayer == null) return;

            isBgmOn = !isBgmOn;
            if (isBgmOn)
            {
                bgmPlayer.Play();
            }
            else
            {
                bgmPlayer.Stop();
            }
        }

        private static System.IO.Stream? GetEmbeddedStream(string fileName)
        {
            var asm = typeof(MainWindow).Assembly;
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase))
                    return asm.GetManifestResourceStream(name)!;
            }
            return null;
        }
    }
}
