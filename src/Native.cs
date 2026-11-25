using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Lisan
{
    internal static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
        public const int WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

        const uint KEYEVENTF_KEYUP = 0x2;
        const byte VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        static bool IsDown(byte vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        static bool AnyModifierDown()
        {
            return IsDown(VK_SHIFT) || IsDown(VK_CONTROL) || IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN);
        }

        /// Wait until the user lets go of the hotkey modifiers, so Ctrl+C is not sent as Ctrl+Alt+C.
        public static async Task WaitForModifiersReleased()
        {
            for (int i = 0; i < 40 && AnyModifierDown(); i++) await Task.Delay(15);
            if (AnyModifierDown())
            {
                foreach (byte vk in new[] { VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN })
                    if (IsDown(vk)) keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
        }

        public static void SendCtrlPlus(byte key)
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
