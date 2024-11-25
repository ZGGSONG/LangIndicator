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
    private readonly double defaultLeft;
    private readonly double defaultTop;
    private Timer? _hiddenTimer;
    private int _lastConversionMode = -1;
    private DispatcherTimer _refreshTimer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTimer();

        // 保存默认位置（桌面右下角）
        defaultLeft = SystemParameters.WorkArea.Width - Width - 10;
        defaultTop = SystemParameters.WorkArea.Height - Height - 10;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        Left = defaultLeft;
        Top = defaultTop;

        MouseLeftButtonDown += (s, e) => DragMove();

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
        var isFullShape = (conversionMode & ImeCmoDeFullShape) != 0;

        StatusText.Text = isChineseMode ? "中" : "英";
        StatusText.Foreground = new SolidColorBrush(isChineseMode ? Colors.LightGreen : Colors.White);
    }

    private void ShowView()
    {
        UpdateWindowPosition();
        Visibility = Visibility.Visible;
        _hiddenTimer?.Change(HiddenDelay, Timeout.Infinite);
    }

    private void UpdateWindowPosition()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            var threadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            var guiInfo = new GUITHREADINFO { cbSize = Marshal.SizeOf(typeof(GUITHREADINFO)) };

            if (GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndCaret != IntPtr.Zero)
            {
                // 有光标，获取光标位置
                var caretRect = guiInfo.rcCaret;
                var point = new POINT { x = caretRect.left, y = caretRect.bottom };

                // 将客户区坐标转换为屏幕坐标
                if (ClientToScreen(guiInfo.hwndCaret, ref point))
                {
                    double newLeft = point.x;
                    double newTop = point.y;

                    // 确保窗口不会超出屏幕范围
                    newLeft = Math.Max(0, Math.Min(newLeft, SystemParameters.WorkArea.Width - Width));
                    newTop = Math.Max(0, Math.Min(newTop, SystemParameters.WorkArea.Height - Height));

                    // 应用新位置
                    Left = newLeft;
                    Top = newTop;
                    return;
                }
            }
        }

        // 如果没有找到光标或出现错误，使用默认位置
        Left = defaultLeft;
        Top = defaultTop;

        ShowAnimation();
    }

    private void ShowAnimation()
    {
        Dispatcher.Invoke(() =>
        {
            var animation = new DoubleAnimation
            {
                From = Top + 10,
                To = Top,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase()
            };

            BeginAnimation(TopProperty, animation);
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
    private const int ImeCmoDeFullShape = 0x8;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);


    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    #endregion
}