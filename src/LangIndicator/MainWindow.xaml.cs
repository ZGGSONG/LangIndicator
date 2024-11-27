﻿using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LangIndicator;

public partial class MainWindow
{
    private readonly Dictionary<StorageMsg, int> StorageDic = new()
    {
        { StorageMsg.ConversionMode, -1 },
        { StorageMsg.CapsLock, -1 },
    };
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
    private static readonly SolidColorBrush UpperCaseBrush = new(Colors.Orange);

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
        var conversionMode = GetImeConversionMode();
        var capsLock = GetCapsLockState();
        if (conversionMode == StorageDic[StorageMsg.ConversionMode]
            && capsLock == StorageDic[StorageMsg.CapsLock])
            return;
        StorageDic[StorageMsg.ConversionMode] = conversionMode;
        StorageDic[StorageMsg.CapsLock] = capsLock;
        UpdateDisplay(conversionMode, capsLock);
    }

    private void UpdateDisplay(int conversionMode, int capsLock)
    {
        var isChineseMode = (conversionMode & IME_CMODE_NATIVE) != 0;
        var isFullShape = (conversionMode & IME_CMODE_FULLSHAPE) != 0;
        var isUpperCase = capsLock != 0;
        //System.Diagnostics.Debug.WriteLine($"转换模式: {conversionMode}, 全半角: {isFullShape}, 大小写: {isUpperCase}");

        var cursorPos = GetCursorPosition();
        Left = cursorPos.Item1;
        Top = cursorPos.Item2;

        if (isUpperCase)
        {
            LangTxt.Text = "EN";
            LangTxt.Foreground = UpperCaseBrush;
        }
        else
        {
            LangTxt.Text = isChineseMode ? "中" : "英";
            LangTxt.Foreground = isChineseMode ? ChineseBrush : EnglishBrush;
        }

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

    /// <summary>
    ///     获取大写锁定键状态
    /// </summary>
    /// <returns></returns>
    public static int GetCapsLockState()
    {
        return GetKeyState(VK_CAPITAL) & 0x0001;
    }

    /// <summary>
    ///     获取当前输入法的转换模式
    /// </summary>
    public static int GetImeConversionMode()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
            return 0;

        var imeWnd = ImmGetDefaultIMEWnd(foregroundWindow);
        if (imeWnd == IntPtr.Zero)
            return 0;

        var result = SendMessage(imeWnd, WM_IME_CONTROL, new IntPtr(IMC_GETCONVERSIONMODE), IntPtr.Zero);
        return result.ToInt32();
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

    /// <summary>
    ///     应用程序使用此消息来控制已创建的 IME 窗口
    /// </summary>
    private const int WM_IME_CONTROL = 0x283;

    /// <summary>
    ///     大写锁定键
    /// </summary>
    private const int VK_CAPITAL = 0x14;

    /// <summary>
    ///     输入法管理器命令
    /// </summary>
    private const int IMC_GETCONVERSIONMODE = 0x001;

    #region IGP_CONVERSION
    // https://www.cnblogs.com/zyl910/archive/2006/06/04/2186644.html

    private const int IME_CMODE_CHINESE = 0x1;          // 中文输入
    private const int IME_CMODE_NATIVE = 0x1;           // 等同于 CHINESE
    private const int IME_CMODE_FULLSHAPE = 0x8;        // 全角
    private const int IME_CMODE_ROMAN = 0x10;           // 罗马字
    private const int IME_CMODE_CHARCODE = 0x20;        // 字符码
    private const int IME_CMODE_HANJACONVERT = 0x40;    // 汉字转换
    private const int IME_CMODE_SOFTKBD = 0x80;         // 软键盘
    private const int IME_CMODE_NOCONVERSION = 0x100;   // 无转换
    private const int IME_CMODE_EUDC = 0x200;           // 用户自定义字符
    private const int IME_CMODE_SYMBOL = 0x400;         // 符号转换
    private const int IME_CMODE_FIXED = 0x800;          // 固定转换

    #endregion

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern int GetKeyState(int vKey);

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

public enum StorageMsg
{
    ConversionMode,
    CapsLock,
}