using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace SPConverter.Views;

/// <summary>Windows 11 style animations; disabled when Windows animation effects are off.</summary>
public static class Animations
{
    private const int DefaultFrameRate = 60;

    /// <summary>Fluent decelerate curve.</summary>
    public static readonly IEasingFunction Decelerate = CreateFrozenEase(0.1, 0.9, 0.2, 1.0);

    /// <summary>Fluent standard curve.</summary>
    public static readonly IEasingFunction Standard = CreateFrozenEase(0.8, 0.0, 0.2, 1.0);

    public static bool Enabled => SystemParameters.ClientAreaAnimation;

    /// <summary>Sets the default animation frame rate to the primary display refresh rate (WPF default is 60).</summary>
    public static void ConfigureFrameRate()
    {
        int refreshRate = Math.Clamp(GetPrimaryDisplayRefreshRate(), 30, 240);
        Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(Timeline),
            new FrameworkPropertyMetadata { DefaultValue = refreshRate });
    }

    public static void PlayEntrance(UIElement element, double offsetY = 28, int durationMs = 350, bool useBitmapCache = false)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);

        if (!Enabled)
        {
            element.Opacity = 1;
            return;
        }

        var translate = EnsureTranslateTransform(element);
        translate.BeginAnimation(TranslateTransform.YProperty, null);

        // Start after the first rendered frame so layout does not consume the animation.
        element.Opacity = 0;
        translate.Y = offsetY;

        RunAfterFirstFrame(() =>
        {
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var fade = new DoubleAnimation(0, 1, duration) { EasingFunction = Decelerate };
            var slide = new DoubleAnimation(offsetY, 0, duration) { EasingFunction = Decelerate };

            // A bitmap cache only helps small static elements.
            if (useBitmapCache)
            {
                EnableBitmapCache(element);
            }

            fade.Completed += (_, _) =>
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Opacity = 1;
                element.CacheMode = null;
            };

            element.BeginAnimation(UIElement.OpacityProperty, fade);
            translate.BeginAnimation(TranslateTransform.YProperty, slide);
        });
    }

    public static void FadeOut(UIElement element, Action onCompleted, int durationMs = 167)
    {
        if (!Enabled)
        {
            onCompleted();
            return;
        }

        EnableBitmapCache(element);
        var animation = new DoubleAnimation(element.Opacity, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = Standard
        };
        animation.Completed += (_, _) =>
        {
            onCompleted();
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = 1;
            element.CacheMode = null;
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    #region Theme crossfade

    private static Image? _crossfadeOverlay;

    /// <summary>Cross-fades from a snapshot of the previous theme.</summary>
    public static void BeginThemeCrossfade(Grid root)
    {
        if (!Enabled || !root.IsLoaded || root.ActualWidth < 1 || root.ActualHeight < 1) return;

        ImageSource snapshot = Snapshot(root);

        RemoveCrossfadeOverlay(root);
        var overlay = new Image
        {
            Source = snapshot,
            Stretch = Stretch.Fill,
            Width = root.ActualWidth,
            Height = root.ActualHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            IsHitTestVisible = false
        };
        Grid.SetRowSpan(overlay, Math.Max(1, root.RowDefinitions.Count));
        Grid.SetColumnSpan(overlay, Math.Max(1, root.ColumnDefinitions.Count));
        Panel.SetZIndex(overlay, int.MaxValue);
        root.Children.Add(overlay);
        _crossfadeOverlay = overlay;

        // Start once the new theme and backdrop are rendered.
        root.Dispatcher.BeginInvoke(
            () => RunAfterFirstFrame(() =>
            {
                if (!ReferenceEquals(_crossfadeOverlay, overlay)) return;

                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = Standard };
                fade.Completed += (_, _) =>
                {
                    if (ReferenceEquals(_crossfadeOverlay, overlay))
                    {
                        RemoveCrossfadeOverlay(root);
                    }
                };
                overlay.BeginAnimation(UIElement.OpacityProperty, fade);
            }),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private static void RemoveCrossfadeOverlay(Grid root)
    {
        if (_crossfadeOverlay == null) return;

        root.Children.Remove(_crossfadeOverlay);
        _crossfadeOverlay = null;
    }

    private static ImageSource Snapshot(FrameworkElement element)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(element);
        int pixelWidth = (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX);
        int pixelHeight = (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY);

        // VisualBrush ignores the element offset that RenderTargetBitmap.Render would include.
        var drawing = new DrawingVisual();
        using (DrawingContext context = drawing.RenderOpen())
        {
            context.DrawRectangle(
                new VisualBrush(element) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                null,
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        bitmap.Freeze();
        return bitmap;
    }

    #endregion

    #region FadeInOnVisible

    public static readonly DependencyProperty FadeInOnVisibleProperty = DependencyProperty.RegisterAttached(
        "FadeInOnVisible",
        typeof(bool),
        typeof(Animations),
        new PropertyMetadata(false, OnFadeInOnVisibleChanged));

    public static bool GetFadeInOnVisible(DependencyObject element) => (bool)element.GetValue(FadeInOnVisibleProperty);

    public static void SetFadeInOnVisible(DependencyObject element, bool value) => element.SetValue(FadeInOnVisibleProperty, value);

    private static void OnFadeInOnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        element.IsVisibleChanged -= OnElementIsVisibleChanged;
        if ((bool)e.NewValue)
        {
            element.IsVisibleChanged += OnElementIsVisibleChanged;
        }
    }

    private static void OnElementIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is UIElement element && (bool)e.NewValue)
        {
            PlayEntrance(element, offsetY: 8, durationMs: 300);
        }
    }

    #endregion

    #region SmoothValue (ProgressBar)

    public static readonly DependencyProperty SmoothValueProperty = DependencyProperty.RegisterAttached(
        "SmoothValue",
        typeof(double),
        typeof(Animations),
        new PropertyMetadata(0d, OnSmoothValueChanged));

    public static double GetSmoothValue(DependencyObject element) => (double)element.GetValue(SmoothValueProperty);

    public static void SetSmoothValue(DependencyObject element, double value) => element.SetValue(SmoothValueProperty, value);

    private static void OnSmoothValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RangeBase range) return;

        double target = (double)e.NewValue;
        double current = range.Value;

        // Resets apply immediately; only forward progress is animated.
        if (!Enabled || target <= current)
        {
            range.BeginAnimation(RangeBase.ValueProperty, null);
            range.Value = target;
            return;
        }

        var animation = new DoubleAnimation(current, target, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = Decelerate
        };
        range.BeginAnimation(RangeBase.ValueProperty, animation);
    }

    #endregion

    /// <summary>Runs the action after one full frame has been rendered.</summary>
    private static void RunAfterFirstFrame(Action action)
    {
        int framesSeen = 0;
        EventHandler? handler = null;
        handler = (_, _) =>
        {
            if (++framesSeen < 2) return;

            CompositionTarget.Rendering -= handler;
            action();
        };
        CompositionTarget.Rendering += handler;
    }

    /// <summary>Caches the element as a bitmap for the duration of an animation.</summary>
    private static void EnableBitmapCache(UIElement element)
    {
        DpiScale dpi = VisualTreeHelper.GetDpi(element);
        element.CacheMode = new BitmapCache
        {
            RenderAtScale = Math.Max(dpi.DpiScaleX, dpi.DpiScaleY),
            SnapsToDevicePixels = true
        };
    }

    private static TranslateTransform EnsureTranslateTransform(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform { IsFrozen: false } existing)
        {
            return existing;
        }

        var translate = new TranslateTransform();
        element.RenderTransform = translate;
        return translate;
    }

    private static IEasingFunction CreateFrozenEase(double x1, double y1, double x2, double y2)
    {
        var easing = new CubicBezierEase(x1, y1, x2, y2);
        easing.Freeze();
        return easing;
    }

    private static int GetPrimaryDisplayRefreshRate()
    {
        var mode = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        return EnumDisplaySettings(null, EnumCurrentSettings, ref mode) && mode.dmDisplayFrequency > 1
            ? mode.dmDisplayFrequency
            : DefaultFrameRate;
    }

    private const int EnumCurrentSettings = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
