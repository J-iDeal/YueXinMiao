using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace 月薪喵
{
    public partial class ImagePickerWindow : Window
    {
        private readonly List<string> imageResources;
        private readonly string? currentResource;
        private readonly Action<string> onSelect;
        private readonly Action? onChangeLocal;
        private readonly Action? onRestoreDefault;
        private readonly bool isDefault;

        public ImagePickerWindow(
            List<string> imageResources,
            string? currentResource,
            Action<string> onSelect,
            Action? onChangeLocal,
            Action? onRestoreDefault)
        {
            this.imageResources = imageResources;
            this.currentResource = currentResource;
            this.onSelect = onSelect;
            this.onChangeLocal = onChangeLocal;
            this.onRestoreDefault = onRestoreDefault;
            this.isDefault = currentResource == "pet.gif";

            Title = "选择宠物图片";
            Width = 480;
            Height = 460;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            Topmost = true;

            MouseLeftButtonDown += (_, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };

            BuildUI();
        }

        private void BuildUI()
        {
            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromRgb(0xFC, 0xFC, 0xFA)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromArgb(0x30, 0x00, 0x00, 0x00),
                    BlurRadius = 40,
                    ShadowDepth = 8,
                    Direction = 270,
                    Opacity = 1
                }
            };

            var rootPanel = new StackPanel();

            // ── 标题栏 ──
            var titleBar = new Border
            {
                Padding = new Thickness(20, 16, 20, 8),
                Background = Brushes.Transparent
            };
            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition());
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "选择宠物图片",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(title, 0);
            titleGrid.Children.Add(title);

            var closeBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE4)),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "x",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            closeBtn.MouseEnter += (_, _) => closeBtn.Background = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xCC));
            closeBtn.MouseLeave += (_, _) => closeBtn.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE4));
            closeBtn.MouseLeftButtonDown += (_, _) => Close();
            Grid.SetColumn(closeBtn, 2);
            titleGrid.Children.Add(closeBtn);

            titleBar.Child = titleGrid;
            rootPanel.Children.Add(titleBar);

            // ── 缩略图网格 ──
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 300,
                Margin = new Thickness(12, 0, 12, 0)
            };

            var wrapPanel = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var asm = typeof(MainWindow).Assembly;
            bool isPetGif = true;

            foreach (var resName in imageResources)
            {
                var card = BuildImageCard(resName, asm, isPetGif);
                wrapPanel.Children.Add(card);
                isPetGif = false;
            }

            scroll.Content = wrapPanel;
            rootPanel.Children.Add(scroll);

            // ── 底部分隔线 ──
            rootPanel.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xEF, 0xEB)),
                Margin = new Thickness(20, 8, 20, 0)
            });

            // ── 底部按钮行 ──
            var btnPanel = new StackPanel
            {
                Margin = new Thickness(20, 10, 20, 16)
            };

            if (onChangeLocal != null)
            {
                btnPanel.Children.Add(BuildBottomButton("更换本地图片", onChangeLocal));
            }

            if (!isDefault && onRestoreDefault != null)
            {
                btnPanel.Children.Add(BuildBottomButton("恢复默认", onRestoreDefault));
            }

            rootPanel.Children.Add(btnPanel);

            outerBorder.Child = rootPanel;
            Content = outerBorder;
        }

        private Border BuildImageCard(string resName, System.Reflection.Assembly asm, bool isPetGif)
        {
            bool isActive = resName == currentResource;

            var card = new Border
            {
                Width = 120,
                Height = 130,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(12),
                Background = isActive
                    ? new SolidColorBrush(Color.FromRgb(0xD8, 0xEA, 0xFF))
                    : new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF2)),
                BorderBrush = isActive
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))
                    : Brushes.Transparent,
                BorderThickness = isActive ? new Thickness(2) : new Thickness(0),
                Cursor = Cursors.Hand,
                Tag = resName
            };

            var stack = new StackPanel();

            // 缩略图
            var img = new Image
            {
                Width = 90,
                Height = 90,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 10, 0, 4)
            };

            try
            {
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream != null)
                {
                    var decoder = new GifBitmapDecoder(stream,
                        BitmapCreateOptions.PreservePixelFormat,
                        BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 0)
                    {
                        var frozen = BitmapFrame.Create(decoder.Frames[0]);
                        frozen.Freeze();
                        img.Source = frozen;
                    }
                }
            }
            catch
            {
                // 静默跳过
            }

            stack.Children.Add(img);

            // 名称
            string display = GetDisplayName(resName);
            if (isPetGif) display = "默认\n月薪喵";

            var label = new TextBlock
            {
                Text = display,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 100,
                Margin = new Thickness(0, 0, 0, 4)
            };
            stack.Children.Add(label);

            card.Child = stack;

            card.MouseEnter += (_, _) =>
            {
                if (!isActive)
                    card.Background = new SolidColorBrush(Color.FromRgb(0xEB, 0xEB, 0xE7));
            };
            card.MouseLeave += (_, _) =>
            {
                if (!isActive)
                    card.Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF2));
            };
            card.MouseLeftButtonDown += (_, _) =>
            {
                onSelect(resName);
                Close();
            };

            return card;
        }

        private static Border BuildBottomButton(string text, Action onClick)
        {
            var btn = new Border
            {
                Padding = new Thickness(16, 10, 16, 10),
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xEE)),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 6),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };

            btn.MouseEnter += (_, _) => btn.Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xDC));
            btn.MouseLeave += (_, _) => btn.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xEE));
            btn.MouseLeftButtonDown += (_, _) => onClick();

            return btn;
        }

        private static string GetDisplayName(string resName)
        {
            var fileName = resName;
            if (fileName.StartsWith("月薪喵.images.", StringComparison.OrdinalIgnoreCase))
                fileName = fileName["月薪喵.images.".Length..];
            else if (fileName.StartsWith("月薪喵.", StringComparison.OrdinalIgnoreCase))
                fileName = fileName["月薪喵.".Length..];

            if (fileName.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^4];

            if (fileName.Length > 14)
                fileName = fileName[..14] + "…";

            return fileName;
        }
    }
}
