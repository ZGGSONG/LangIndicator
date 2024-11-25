using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace LangIndicator;

public partial class MainWindow
{
    // Win32 API 常量
    private const int WM_IME_CONTROL = 0x283;
    private const int IMC_GETCONVERSIONMODE = 0x001;
    private const int IME_CMODE_NATIVE = 0x1;
    private const int IME_CMODE_FULLSHAPE = 0x8;
    private int lastConversionMode = -1;

    private TextBlock statusText;
    private DispatcherTimer timer;

    public MainWindow()
    {
        InitializeComponent();
        InitializeWindowStyle();
        InitializeTimer();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private void InitializeWindowStyle()
    {
        Topmost = true;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Width = 60;
        Height = 30;

        Left = SystemParameters.WorkArea.Width - Width - 10;
        Top = SystemParameters.WorkArea.Height - Height - 10;

        statusText = new TextBlock
        {
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var mainGrid = new Grid
        {
            Background = new SolidColorBrush(Colors.Black) { Opacity = 0.7 }
        };
        mainGrid.Children.Add(statusText);
        Content = mainGrid;

        MouseLeftButtonDown += (s, e) => DragMove();
    }

    private void InitializeTimer()
    {
        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += Timer_Tick;
        timer.Start();
    }

    private void Timer_Tick(object sender, EventArgs e)
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

                    // 调试信息
                    Console.WriteLine($"Conversion Mode: {conversionMode}");
                }
            }
        }
    }

    private void UpdateStatusDisplay(int conversionMode)
    {
        var isChineseMode = (conversionMode & IME_CMODE_NATIVE) != 0;
        var isFullShape = (conversionMode & IME_CMODE_FULLSHAPE) != 0;

        statusText.Text = isChineseMode ? "中" : "英";
        statusText.Foreground = new SolidColorBrush(isChineseMode ? Colors.LightGreen : Colors.White);

        // 调试信息
        Console.WriteLine($"Chinese Mode: {isChineseMode}, Full Shape: {isFullShape}");
    }

    protected override void OnClosed(EventArgs e)
    {
        timer?.Stop();
        base.OnClosed(e);
    }
}