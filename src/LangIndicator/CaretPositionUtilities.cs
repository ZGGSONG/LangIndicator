using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LangIndicator;

/// <summary>
///     <see href="https://learn.microsoft.com/en-us/answers/questions/818647/how-to-get-caret-position-in-any-application-from"/>
///     * 仅适用于 WinForms/WPF 程序
/// </summary>
public static class CaretPositionUtilities
{
    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    public static (bool hasCaretPosition, Point position) GetCaretPosition()
    {
        try
        {
            var guiInfo = new GUITHREADINFO();
            guiInfo.cbSize = Marshal.SizeOf(guiInfo);

            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return (false, new Point());

            var threadId = GetWindowThreadProcessId(foregroundWindow, IntPtr.Zero);
            if (GetGUIThreadInfo(threadId, ref guiInfo))
                if (guiInfo.hwndCaret != IntPtr.Zero)
                {
                    var point = guiInfo.rcCaret.Location;
                    if (ClientToScreen(guiInfo.hwndCaret, ref point))
                        return (true, point);
                }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting caret position: {ex.Message}");
        }

        return (false, new Point());
    }

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
        public Rectangle rcCaret;
    }
}