using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

namespace LangIndicator;

public static class WindowStyleUtilities
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

    [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
    private static extern void SetLastError(int dwErrorCode);

    private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        SetLastError(0); // Clear any existing error

        if (IntPtr.Size == 4)
        {
            return new IntPtr(IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong)));
        }

        return IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
    }

    private static int IntPtrToInt32(IntPtr intPtr)
    {
        return unchecked((int)intPtr.ToInt64());
    }

    /// <summary>
    /// 从Alt+Tab窗口列表中隐藏窗口
    /// </summary>
    /// <param name="window">要隐藏的窗口</param>
    public static void HideFromAltTab(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE).ToInt32();

        // 添加 TOOLWINDOW 样式，移除 APPWINDOW 样式
        exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;

        SetWindowLong(helper.Handle, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// 恢复窗口在Alt+Tab窗口列表中的显示
    /// </summary>
    /// <param name="window">要恢复显示的窗口</param>
    public static void ShowInAltTab(Window window)
    {
        var helper = new WindowInteropHelper(window);
        var exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE).ToInt32();

        // 移除 TOOLWINDOW 样式，添加 APPWINDOW 样式
        exStyle = (exStyle & ~WS_EX_TOOLWINDOW) | WS_EX_APPWINDOW;

        SetWindowLong(helper.Handle, GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// 获取窗口当前的扩展样式
    /// </summary>
    /// <param name="window">要获取样式的窗口</param>
    /// <returns>当前的扩展样式值</returns>
    public static int GetCurrentWindowStyle(Window window)
    {
        var helper = new WindowInteropHelper(window);
        return GetWindowLong(helper.Handle, GWL_EXSTYLE).ToInt32();
    }
}
