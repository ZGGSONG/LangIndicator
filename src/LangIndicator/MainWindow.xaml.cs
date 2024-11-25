using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LangIndicator;

public partial class MainWindow
{
    private const int HiddenDelay = 2000;
    private const int RefreshDelay = 100;
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

        base.OnSourceInitialized(e);
    }

    [MemberNotNull(nameof(_refreshTimer))]
    private void InitializeTimer()
    {
        _hiddenTimer = new Timer(AutoHidden, null, Timeout.Infinite, Timeout.Infinite);

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
        UpdateStatusDisplay(conversionMode);

        ShowView();
    }

    private void UpdateStatusDisplay(int conversionMode)
    {
        var isChineseMode = (conversionMode & ImeCmoDeNative) != 0;
        // var isFullShape = (conversionMode & ImeCmoDeFullShape) != 0;

        StatusText.Text = isChineseMode ? "中" : "英";
        StatusText.Foreground = new SolidColorBrush(isChineseMode ? Colors.LightGreen : Colors.White);
    }

    private void ShowView()
    {
        UpdateWindowPosition();
        Visibility = Visibility.Visible;
        _hiddenTimer?.Change(HiddenDelay, Timeout.Infinite);
    }
    
    private ValueTuple<double, double> GetCursorPosition()
    {
        GetCursorPos(out Point point);

        var source = PresentationSource.FromVisual(Application.Current.MainWindow!);
        if (source?.CompositionTarget == null) return new ValueTuple<double, double>(point.X, point.Y);
        var transformToDevice = source.CompositionTarget.TransformToDevice;
        
        return new ValueTuple<double, double>(point.X / transformToDevice.M11, point.Y / transformToDevice.M22);
    }

    private void UpdateWindowPosition()
    {
        var point = GetCursorPosition();
        Left = point.Item1;
        Top = point.Item2;
        ShowAnimation();
    }

    private void ShowAnimation()
    {
        Dispatcher.Invoke(() =>
        {
            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                FillBehavior = FillBehavior.Stop
            };
            var topAnimation = new DoubleAnimation
            {
                From = Top + 10,
                To = Top,
                Duration = TimeSpan.FromMilliseconds(200),
                FillBehavior = FillBehavior.Stop
            };
        
            BeginAnimation(OpacityProperty, opacityAnimation);
            BeginAnimation(TopProperty, topAnimation);
        });
    }

    private void AutoHidden(object? state)
    {
        Dispatcher.Invoke(() => Visibility = Visibility.Collapsed);
    }

    protected override void OnClosed(EventArgs e)
    {
        _refreshTimer.Stop();
        base.OnClosed(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }


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