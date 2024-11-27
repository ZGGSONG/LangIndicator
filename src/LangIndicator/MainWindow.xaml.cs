using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LangIndicator;

public partial class MainWindow
{
    private int _lastConversionMode = -1;
    private const int HiddenDelay = 1000;
    private const int RefreshDelay = 100;
    private const int AnimationDuration = 200;
    private readonly Timer _hiddenTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly IDisposable _gcHandle;

    // 缓存常用的画笔和动画
    private static readonly SolidColorBrush ChineseBrush = new(Colors.LightGreen);
    private static readonly SolidColorBrush EnglishBrush = new(Colors.White);
    private static readonly CircleEase CircleEaseAnimation = new() { EasingMode = EasingMode.EaseInOut };


    public MainWindow()
    {
        InitializeComponent();
        _hiddenTimer = new Timer(HideHandle, null, Timeout.Infinite, Timeout.Infinite);
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(RefreshDelay)
        };

        _gcHandle = MemoUtilities.StartPeriodicGC(true);
        InitializeWindow();
    }

    private void InitializeWindow()
    {
        _refreshTimer.Tick += RefreshTimerTick;
        _refreshTimer.Start();

        Loaded += OnLoaded;
        MouseLeftButtonDown += (_, _) => DragMove();
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Width - Width - 10;
        Top = SystemParameters.WorkArea.Height - Height - 10;
        Visibility = Visibility.Collapsed;

        Startup.IsChecked = ShortcutUtilities.IsStartup();
    }

    private void RefreshTimerTick(object? sender, EventArgs e)
    {
        CheckImeStatus();
    }

    private void CheckImeStatus()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero) return;
        var defaultImeWnd = ImmGetDefaultIMEWnd(foregroundWindow);
        if (defaultImeWnd == IntPtr.Zero) return;
        // 发送消息获取转换模式
        var result = SendMessage(defaultImeWnd, WmImeControl, new IntPtr(ImcGetConversionMode), IntPtr.Zero);
        var conversionMode = result.ToInt32();
        if (conversionMode == _lastConversionMode) return;
        _lastConversionMode = conversionMode;
        UpdateDisplay(conversionMode);
    }

    private void UpdateDisplay(int conversionMode)
    {
        var isChineseMode = (conversionMode & ImeCmoDeNative) != 0;

        var cursorPos = GetCursorPosition();
        Left = cursorPos.Item1;
        Top = cursorPos.Item2;

        LangTxt.Text = isChineseMode ? "中" : "英";
        LangTxt.Foreground = isChineseMode ? ChineseBrush : EnglishBrush;

        // 动画显示
        Visibility = Visibility.Visible;
        LangIndicator.Opacity = 0;
        LangTxt.Opacity = 0;
        var duration = TimeSpan.FromMilliseconds(AnimationDuration);
        var txtOpacity = new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.HoldEnd };
        var windowOpacity = new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.HoldEnd };
        var windowMotion = new DoubleAnimation(Top + 10, Top, duration)
        {
            EasingFunction = CircleEaseAnimation,
            FillBehavior = FillBehavior.Stop
        };
        LangTxt.BeginAnimation(OpacityProperty, txtOpacity);
        LangIndicator.BeginAnimation(OpacityProperty, windowOpacity);
        LangIndicator.BeginAnimation(TopProperty, windowMotion);

        _hiddenTimer.Change(HiddenDelay, Timeout.Infinite);
    }

    private void HideHandle(object? state)
    {
        Dispatcher.Invoke(() =>
        {
            LangTxt.Opacity = 0;
            LangIndicator.Opacity = 0;
            Visibility = Visibility.Collapsed;
        });
    }

    private (double, double) GetCursorPosition()
    {
        GetCursorPos(out var point);

        var source = PresentationSource.FromVisual(Application.Current.MainWindow!);
        if (source?.CompositionTarget == null) return (point.X, point.Y);
        var transformToDevice = source.CompositionTarget.TransformToDevice;

        var x = point.X / transformToDevice.M11;
        var y = point.Y / transformToDevice.M22;

        // 确保窗口不会超出屏幕边界
        var screenWidth = SystemParameters.WorkArea.Width;
        var screenHeight = SystemParameters.WorkArea.Height;

        x = Math.Min(screenWidth - Width, x);
        y = Math.Min(screenHeight - Height, y);

        return (x, y);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hiddenTimer.Dispose();
        _refreshTimer.Stop();
        _gcHandle.Dispose();
        base.OnClosed(e);
    }

    #region NotiryIcon

    private void Startup_OnClick(object sender, RoutedEventArgs e)
    {
        if (ShortcutUtilities.IsStartup())
        {
            ShortcutUtilities.UnSetStartup();
            Startup.IsChecked = false;
        }
        else
        {
            ShortcutUtilities.SetStartup();
            Startup.IsChecked = true;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Win32 API

    private const int WmImeControl = 0x283;
    private const int ImcGetConversionMode = 0x001;
    private const int ImeCmoDeNative = 0x1;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);


    [StructLayout(LayoutKind.Sequential)]
    private struct Point(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point lpPoint);

    #endregion
}