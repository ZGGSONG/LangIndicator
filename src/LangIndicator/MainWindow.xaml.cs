using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LangIndicator;

public partial class MainWindow
{
    private const int HiddenDelay = 1000;
    private const int RefreshDelay = 100;
    private const int AnimationDuration = 200;
    private Timer? _hiddenTimer;
    private int _lastConversionMode = -1;
    private DispatcherTimer _refreshTimer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTimer();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        Left = SystemParameters.WorkArea.Width - Width - 10;
        Top = SystemParameters.WorkArea.Height - Height - 10;

        MouseLeftButtonDown += (_, _) => DragMove();

        Visibility = Visibility.Collapsed;

        Startup.IsChecked = ShortcutUtilities.IsStartup();

        base.OnSourceInitialized(e);
    }

    [MemberNotNull(nameof(_refreshTimer))]
    private void InitializeTimer()
    {
        _hiddenTimer = new Timer(HideHandle, null, Timeout.Infinite, Timeout.Infinite);

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RefreshDelay)
        };
        _refreshTimer.Tick += RefreshTimerTick;
        _refreshTimer.Start();
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

        var point = GetCursorPosition();
        Left = point.Item1;
        Top = point.Item2;

        LangTxt.Text = isChineseMode ? "中" : "英";
        LangTxt.Foreground = new SolidColorBrush(isChineseMode ? Colors.LightGreen : Colors.White);

        var easing = new CircleEase
        {
            EasingMode = EasingMode.EaseInOut
        };
        var windowOpacity = new DoubleAnimation
        {
            From = 0,
            To = 1,
            EasingFunction = easing,
            Duration = TimeSpan.FromMilliseconds(AnimationDuration),
            FillBehavior = FillBehavior.Stop
        };
        var windowMotion = new DoubleAnimation
        {
            From = Top + 10,
            To = Top,
            EasingFunction = easing,
            Duration = TimeSpan.FromMilliseconds(AnimationDuration),
            FillBehavior = FillBehavior.Stop
        };
        BeginAnimation(OpacityProperty, windowOpacity);
        BeginAnimation(TopProperty, windowMotion);
        Visibility = Visibility.Visible;

        _hiddenTimer?.Change(HiddenDelay, Timeout.Infinite);
    }

    private ValueTuple<double, double> GetCursorPosition()
    {
        GetCursorPos(out var point);

        var source = PresentationSource.FromVisual(Application.Current.MainWindow!);
        if (source?.CompositionTarget == null) return new ValueTuple<double, double>(point.X, point.Y);
        var transformToDevice = source.CompositionTarget.TransformToDevice;

        var x = point.X / transformToDevice.M11;
        var y = point.Y / transformToDevice.M22;

        // 获取屏幕工作区的宽度和高度
        var screenWidth = SystemParameters.WorkArea.Width;
        var screenHeight = SystemParameters.WorkArea.Height;

        // 判断是否到达屏幕右下方边缘
        if (x + Width > screenWidth)
        {
            x = screenWidth - Width;
        }

        if (y + Height > screenHeight)
        {
            y = screenHeight - Height;
        }

        return new ValueTuple<double, double>(x, y);
    }

    private void HideHandle(object? state)
    {
        Dispatcher.Invoke(() => Visibility = Visibility.Collapsed);
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
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