using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LangIndicator;

public partial class MainWindow
{
    private int lastConversionMode = -1;
    private DispatcherTimer timer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTimer();
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        Left = SystemParameters.WorkArea.Width - Width - 10;
        Top = SystemParameters.WorkArea.Height - Height - 10;

        MouseLeftButtonDown += (s, e) => DragMove();

        Visibility = Visibility.Collapsed;

        base.OnSourceInitialized(e);
    }

    [MemberNotNull(nameof(timer))]
    private void InitializeTimer()
    {
        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += Timer_Tick;
        timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        CheckImeStatus();
    }

    private void CheckImeStatus()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow != IntPtr.Zero)
        {
            var defaultImeWnd = ImmGetDefaultIMEWnd(foregroundWindow);
            if (defaultImeWnd != IntPtr.Zero)
            {
                // 发送消息获取转换模式
                var result = SendMessage(defaultImeWnd, WM_IME_CONTROL, new IntPtr(IMC_GETCONVERSIONMODE), IntPtr.Zero);
                var conversionMode = result.ToInt32();

                if (conversionMode != lastConversionMode)
                {
                    lastConversionMode = conversionMode;
                    UpdateStatusDisplay(conversionMode);
                    
                    Visibility = Visibility.Visible;

                    Task.Run(async () =>
                    {
                        await Task.Delay(3000);

                        Dispatcher.Invoke(() => Visibility = Visibility.Collapsed);
                    });

                    // 调试信息
                    //Console.WriteLine($"Conversion Mode: {conversionMode}");
                }
            }
        }
    }

    private void UpdateStatusDisplay(int conversionMode)
    {
        var isChineseMode = (conversionMode & IME_CMODE_NATIVE) != 0;
        var isFullShape = (conversionMode & IME_CMODE_FULLSHAPE) != 0;

        StatusText.Text = isChineseMode ? "中" : "英";
        StatusText.Foreground = new SolidColorBrush(isChineseMode ? Colors.LightGreen : Colors.White);

        // 调试信息
        Console.WriteLine($"Chinese Mode: {isChineseMode}, Full Shape: {isFullShape}");
    }

    protected override void OnClosed(EventArgs e)
    {
        timer?.Stop();
        base.OnClosed(e);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }


    #region Win32 API

    private const int WM_IME_CONTROL = 0x283;
    private const int IMC_GETCONVERSIONMODE = 0x001;
    private const int IME_CMODE_NATIVE = 0x1;
    private const int IME_CMODE_FULLSHAPE = 0x8;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    #endregion
}