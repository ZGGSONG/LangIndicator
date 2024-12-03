using System.Configuration;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LangIndicator;

public partial class MainWindow
{
    public static class Config
    {
        public static bool IsStartup;
        public static bool IsShowFullShape;
        public static bool IsShowSymbol;
        public static double HiddenDelayMultiplier;
    }

    private static class Constant
    {
        public const int HiddenDelay = 1000;
        public const int RefreshDelay = 100;
        public const int AnimationDuration = 200;
        public const double WindowOffset = 10.0;
        public const string ChineseIcon = "\xe696";
        public const string EnglishIcon = "\xe695";
        public const string UpperCaseIcon = "\xe603";
        public const string FullShapeIcon = "\xe62c";
        public const string HalfShapeIcon = "\xe6f7";
        public const string SymbolIcon = "\xe637";
    }

    // 缓存动画对象以提高性能
    private readonly DoubleAnimation _txtOpacityAnimation;
    private readonly DoubleAnimation _windowOpacityAnimation;
    private readonly DoubleAnimation _windowMotionAnimation;

    // 缓存常用的画笔和动画
    private static readonly SolidColorBrush UpperCaseBrush = new(Colors.LightSalmon);
    private static readonly SolidColorBrush ChineseBrush = new(Colors.LightGreen);
    private static readonly SolidColorBrush EnglishBrush = new(Colors.White);
    private static readonly CircleEase CircleEaseAnimation = new() { EasingMode = EasingMode.EaseInOut };

    private readonly Dictionary<StorageMsg, int> _storageDic = new()
    {
        { StorageMsg.ConversionMode, -1 },
        { StorageMsg.CapsLock, -1 },
    };
    private readonly Timer _hiddenTimer;
    private readonly DispatcherTimer _refreshTimer;
    private readonly IDisposable _gcHandle;

    public MainWindow()
    {
        InitializeComponent();
        InitializeConfig();

        // 初始化动画对象
        var duration = TimeSpan.FromMilliseconds(Constant.AnimationDuration);
        _txtOpacityAnimation = new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.HoldEnd };
        _windowOpacityAnimation = new DoubleAnimation(0, 1, duration) { FillBehavior = FillBehavior.HoldEnd };
        _windowMotionAnimation = new DoubleAnimation
        {
            Duration = duration,
            EasingFunction = CircleEaseAnimation,
            FillBehavior = FillBehavior.Stop
        };

        _hiddenTimer = new Timer(HideHandle, null, Timeout.Infinite, Timeout.Infinite);
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(Constant.RefreshDelay)
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
        WindowStyleUtilities.HideFromAltTab(this);

        Left = SystemParameters.WorkArea.Width - Width - Constant.WindowOffset;
        Top = SystemParameters.WorkArea.Height - Height - Constant.WindowOffset;
        Visibility = Visibility.Collapsed;

        Startup.IsChecked = ShortcutUtilities.IsStartup();

        var version = Application.ResourceAssembly.GetName().Version?.ToString() ?? "0.0.1";
        NotifyIcon.ToolTipText += FormatVersion(version);
    }

    private void RefreshTimerTick(object? sender, EventArgs e)
    {
        CheckImeStatus();
    }

    private void CheckImeStatus()
    {
        try
        {
            var (conversionMode, capsLock) = (GetImeConversionMode(), GetCapsLockState());
            if (conversionMode == _storageDic[StorageMsg.ConversionMode] && capsLock == _storageDic[StorageMsg.CapsLock])
                return;

            _storageDic[StorageMsg.ConversionMode] = conversionMode;
            _storageDic[StorageMsg.CapsLock] = capsLock;
            UpdateDisplay(conversionMode, capsLock);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking IME status: {ex.Message}");
        }
    }

    private void UpdateDisplay(int conversionMode, int capsLock)
    {
        var (x, y) = GetCursorPosition();
        Left = x;
        Top = y;

        UpdateIndicatorText(conversionMode, capsLock);
        ShowAnimations(y);

        _hiddenTimer.Change((int)(Constant.HiddenDelay * Config.HiddenDelayMultiplier), Timeout.Infinite);
    }

    private void UpdateIndicatorText(int conversionMode, int capsLock)
    {
        var isChineseMode = (conversionMode & IME_CMODE_NATIVE) != 0;
        var isFullShape = (conversionMode & IME_CMODE_FULLSHAPE) != 0;
        var isUpperCase = capsLock != 0;
        var isSymbolMode = (conversionMode & IME_CMODE_SYMBOL) != 0;

        LangTxt.Text = $"{(isUpperCase ? Constant.UpperCaseIcon : (isChineseMode ? Constant.ChineseIcon : Constant.EnglishIcon))}" +
                       $"{(ShowShape.IsChecked ? $" /{(isFullShape ? Constant.FullShapeIcon : Constant.HalfShapeIcon)}" : "")}" +
                       $"{(isUpperCase ? "" : (ShowSymbol.IsChecked ? $"{(isSymbolMode ? $"/{Constant.SymbolIcon}" : "")}" : ""))}";
        LangTxt.Foreground = isUpperCase ? UpperCaseBrush : (isChineseMode ? ChineseBrush : EnglishBrush);
    }

    private void ShowAnimations(double targetTop)
    {
        Visibility = Visibility.Visible;
        LangIndicator.Opacity = 0;
        LangTxt.Opacity = 0;

        _windowMotionAnimation.From = targetTop + Constant.WindowOffset;
        _windowMotionAnimation.To = targetTop;

        LangTxt.BeginAnimation(OpacityProperty, _txtOpacityAnimation);
        LangIndicator.BeginAnimation(OpacityProperty, _windowOpacityAnimation);
        LangIndicator.BeginAnimation(TopProperty, _windowMotionAnimation);
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

        var screens = WpfScreenHelper.Screen.AllScreens;
        var currentScreen = screens.FirstOrDefault(x => x.Bounds.Contains(point.X, point.Y)) ?? screens.First();

        var x = point.X / currentScreen.ScaleFactor;
        var y = point.Y / currentScreen.ScaleFactor;

        // 确保窗口不会超出屏幕边界
        var screenWidth = currentScreen.WpfWorkingArea.Width;
        var screenHeight = currentScreen.WpfWorkingArea.Height;

        x = Math.Min(screenWidth - Width, x);
        y = Math.Min(screenHeight - Height, y);

        return (x, y);
    }
    public string FormatVersion(string version)
    {
        var ver = new Version(version);
        return $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    /// <summary>
    /// 获取大写锁定键状态
    /// </summary>
    /// <returns></returns>
    public static int GetCapsLockState()
    {
        return GetKeyState(VK_CAPITAL) & 0x0001;
    }

    /// <summary>
    /// 获取当前输入法的转换模式
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

    private void InitializeConfig()
    {
        Config.IsStartup = GetConfigValue(nameof(Config.IsStartup));
        Config.IsShowFullShape = GetConfigValue(nameof(Config.IsShowFullShape));
        Config.IsShowSymbol = GetConfigValue(nameof(Config.IsShowSymbol));
        if (!double.TryParse(ConfigurationManager.AppSettings.Get(nameof(Config.HiddenDelayMultiplier)), out Config.HiddenDelayMultiplier))
        {
            Config.HiddenDelayMultiplier = 1.0;
        }

        if (Config.IsStartup && !ShortcutUtilities.IsStartup())
        {
            ShortcutUtilities.SetStartup();
        }
        Startup.IsChecked = Config.IsStartup;
        ShowSymbol.IsChecked = Config.IsShowSymbol;
        ShowShape.IsChecked = Config.IsShowFullShape;
        ShowShape.IsChecked = Config.IsShowFullShape;
        SetHiddenDelayMultiplier(Config.HiddenDelayMultiplier);
    }

    private void SetHiddenDelayMultiplier(double value)
    {
        Config.HiddenDelayMultiplier = value;

        MI0dot6.IsChecked = false;
        MI0dot8.IsChecked = false;
        MI1dot0.IsChecked = false;
        MI1dot2.IsChecked = false;
        MI1dot4.IsChecked = false;
        MI1dot6.IsChecked = false;
        MI2dot0.IsChecked = false;
        MI3dot0.IsChecked = false;
        switch (value)
        {
            case 0.6:
                MI0dot6.IsChecked = true;
                break;
            case 0.8:
                MI0dot8.IsChecked = true;
                break;
            case 1.0:
                MI1dot0.IsChecked = true;
                break;
            case 1.2:
                MI1dot2.IsChecked = true;
                break;
            case 1.4:
                MI1dot4.IsChecked = true;
                break;
            case 1.6:
                MI1dot6.IsChecked = true;
                break;
            case 2.0:
                MI2dot0.IsChecked = true;
                break;
            case 3.0:
                MI3dot0.IsChecked = true;
                break;
            default:
                MI1dot0.IsChecked = true;
                break;

        }

        SaveConfig();
    }

    private static bool GetConfigValue(string key)
    {
        return bool.TryParse(ConfigurationManager.AppSettings.Get(key), out var value) && value;
    }

    private void SaveConfig()
    {
        var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        var settings = config.AppSettings.Settings;

        SaveConfigValue(settings, nameof(Config.IsStartup), Config.IsStartup);
        SaveConfigValue(settings, nameof(Config.IsShowSymbol), Config.IsShowSymbol);
        SaveConfigValue(settings, nameof(Config.IsShowFullShape), Config.IsShowFullShape);
        SaveConfigValue(settings, nameof(Config.HiddenDelayMultiplier), Config.HiddenDelayMultiplier);

        config.Save(ConfigurationSaveMode.Modified);
        ConfigurationManager.RefreshSection("appSettings");
    }

    private static void SaveConfigValue<T>(KeyValueConfigurationCollection settings, string key, T value)
    {
        if (settings[key] != null)
            settings[key].Value = value?.ToString() ?? "";
        else
            settings.Add(key, value?.ToString() ?? "");
    }

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
        Config.IsStartup = Startup.IsChecked;
        SaveConfig();
    }

    private void ShowSymbol_Click(object sender, RoutedEventArgs e)
    {
        ToggleConfigOption(ref Config.IsShowSymbol, ShowSymbol);
    }

    private void ShowShape_Click(object sender, RoutedEventArgs e)
    {
        ToggleConfigOption(ref Config.IsShowFullShape, ShowShape);
    }

    private void MI0dot6_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(0.6);
    }

    private void MI0dot8_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(0.8);
    }

    private void MI1dot0_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(1.0);
    }

    private void MI1dot2_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(1.2);
    }

    private void MI1dot4_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(1.4);
    }

    private void MI1dot6_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(1.6);
    }

    private void MI2dot0_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(2.0);
    }

    private void MI3dot0_Click(object sender, RoutedEventArgs e)
    {
        SetHiddenDelayMultiplier(3.0);
    }

    private void ToggleConfigOption(ref bool configOption, MenuItem item)
    {
        item.IsChecked = !item.IsChecked;
        configOption = item.IsChecked;
        SaveConfig();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #region Win32 API

    /// <summary>
    /// 应用程序使用此消息来控制已创建的 IME 窗口
    /// </summary>
    private const int WM_IME_CONTROL = 0x283;

    /// <summary>
    /// 大写锁定键
    /// </summary>
    private const int VK_CAPITAL = 0x14;

    /// <summary>
    /// 输入法管理器命令
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
    private struct Point
    {
        public int X;
        public int Y;
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
