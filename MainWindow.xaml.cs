using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
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

                    this.Width = gifFrames[0].PixelWidth;
                    this.Height = gifFrames[0].PixelHeight;

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
    }
}
