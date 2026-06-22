using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace 桌面萌宠
{
    public partial class MainWindow : Window
    {
        private bool isTopmost = true;
        private List<BitmapImage> gifFrames = new List<BitmapImage>();
        private int currentFrame = 0;
        private DispatcherTimer? timer;

        // 记录基准大小（首次加载时图片的大小）
        private double baseWidth = 120;
        private double baseHeight = 120;

        // 当前缩放比例
        private double currentPercentage = 1.0;
        // 保存所有百分比菜单项的引用
        private List<MenuItem>? percentageMenuItems;

        public MainWindow()
        {
            InitializeComponent();

            // 设置初始位置（单位：像素）
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width;   // 距离屏幕左侧
            this.Top = 0;    // 距离屏幕顶部

            LoadGif("pet.gif");
        }

        // 加载 GIF 并分解为帧
        private void LoadGif(string filePath)
        {
            try
            {
                var decoder = new GifBitmapDecoder(
                    new Uri(filePath, UriKind.Relative),
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.Default);

                gifFrames.Clear();
                foreach (var frame in decoder.Frames)
                {
                    var bitmap = new BitmapImage();
                    using (var stream = new System.IO.MemoryStream())
                    {
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(frame));
                        encoder.Save(stream);
                        stream.Position = 0;
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                    }
                    gifFrames.Add(bitmap);
                }

                if (gifFrames.Count > 0)
                {
                    PetImage.Source = gifFrames[0];
                    currentFrame = 0;

                    // 记录图片的原始尺寸作为基准
                    baseWidth = gifFrames[0].PixelWidth;
                    baseHeight = gifFrames[0].PixelHeight;

                    // 设置窗口大小为 100%（基准大小）
                    currentPercentage = 1.0;
                    SetSize(currentPercentage);

                    // 收集所有百分比菜单项
                    percentageMenuItems = new List<MenuItem>();
                    var sizeMenu = FindMenuItem(this.ContextMenu, "📐 大小");
                    if (sizeMenu != null)
                    {
                        foreach (var item in sizeMenu.Items)
                        {
                            var menuItem = item as MenuItem;
                            if (menuItem != null && menuItem.Tag != null)
                            {
                                percentageMenuItems.Add(menuItem);
                            }
                        }
                    }
                    UpdateMenuCheckmarks();

                    StartAnimation(GetGifDelay());
                }
            }
            catch (Exception ex)
            {
                PetImage.Source = null;
                PetImage.Visibility = Visibility.Collapsed;
                MessageBox.Show($"GIF 加载失败: {ex.Message}\n请检查 pet.gif 文件是否存在。");
            }
        }

        // 查找指定 Header 的 MenuItem
        private MenuItem? FindMenuItem(ContextMenu? menu, string header)
        {
            if (menu == null) return null;
            foreach (var item in menu.Items)
            {
                var menuItem = item as MenuItem;
                if (menuItem != null && menuItem.Header.ToString() == header)
                {
                    return menuItem;
                }
            }
            return null;
        }
        
        // 更新菜单中的 ✓ 标记
        private void UpdateMenuCheckmarks()
        {
            if (percentageMenuItems == null) return;

            foreach (var item in percentageMenuItems)
            {
                if (item.Tag != null)
                {
                    double percentage = Convert.ToDouble(item.Tag);
                    // 如果当前比例匹配，显示 ✓，否则显示纯数字
                    if (Math.Abs(percentage - currentPercentage) < 0.001)
                    {
                        item.Header = $"✓ {(int)(percentage * 100)}%";
                    }
                    else
                    {
                        item.Header = $"{(int)(percentage * 100)}%";
                    }
                }
            }
        }

        // 设置大小百分比
        private void SetSize(double percentage)
        {
            currentPercentage = percentage;

            // 使用基准大小乘以百分比
            double newWidth = baseWidth * percentage;
            double newHeight = baseHeight * percentage;

            // 限制最小尺寸，防止太小无法操作
            if (newWidth < 30) newWidth = 40;
            if (newHeight < 30) newHeight = 40;

            // 限制最大尺寸，防止太大超出屏幕
            double maxWidth = SystemParameters.PrimaryScreenWidth * 0.8;
            double maxHeight = SystemParameters.PrimaryScreenHeight * 0.8;
            if (newWidth > maxWidth) newWidth = maxWidth;
            if (newHeight > maxHeight) newHeight = maxHeight;

            this.Width = newWidth;
            this.Height = newHeight;

            // 手动设置大小后，取消自动适应
            this.SizeToContent = SizeToContent.Manual;

            // 更新菜单 ✓ 标记
            UpdateMenuCheckmarks();
        }

        // 百分比菜单点击事件
        private void SizePercentage_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem != null && menuItem.Tag != null)
            {
                double percentage = Convert.ToDouble(menuItem.Tag);
                SetSize(percentage);
            }
        }

        // 获取 GIF 帧延迟时间
        private int GetGifDelay()
        {
            return 100;
        }

        // 开始动画
        private void StartAnimation(int delayMs)
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(delayMs);
            timer.Tick += (s, e) =>
            {
                if (gifFrames.Count > 0)
                {
                    currentFrame = (currentFrame + 1) % gifFrames.Count;
                    PetImage.Source = gifFrames[currentFrame];
                }
            };
            timer.Start();
        }

        // 窗口拖动
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        // 右键菜单
        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 由 ContextMenu 自动处理
        }

        // 重置位置
        private void ResetPosition_Click(object sender, RoutedEventArgs e)
        {
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
        }

        // 切换置顶
        private void ToggleTopmost_Click(object sender, RoutedEventArgs e)
        {
            isTopmost = !isTopmost;
            this.Topmost = isTopmost;

            var menuItem = sender as MenuItem;
            if (menuItem != null)
            {
                menuItem.Header = isTopmost ? "📌 取消置顶" : "📌 开启置顶";
            }
        }

        // 退出
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}