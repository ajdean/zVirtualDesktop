using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace zVirtualDesktop
{
    public class Hooky
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);


        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x100;
        private const int WM_KEYUP = 0x101;
        private const int WM_SYSKEYDOWN = 0x104;
        private const int WM_SYSKEYUP = 0x105;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        private static bool CONTROL_DOWN = false;
        private static bool SHIFT_DOWN = false;
        private static bool ALT_DOWN = false;
        private static bool WIN_DOWN = false;

        [StructLayout(LayoutKind.Sequential)]
        public class KeyboardHookStruct
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        public static readonly List<Shortcut> Shortcuts = new List<Shortcut>();

        public static void Start()
        {
            _hookID = SetHook(_proc);
        }
        
        public static void Stop()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            Debug.WriteLine($"{wParam} {lParam}");
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                //var key = (Keys)Marshal.ReadInt32(lParam);
                var keyboardHookStruct = (KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));
                var key = (Keys)keyboardHookStruct.vkCode;
                Debug.WriteLine($"{key} {keyboardHookStruct.flags}");
                //ALT_DOWN = keyboardHookStruct.flags != 0;
                switch (key)
                {
                    case Keys.LControlKey:
                        CONTROL_DOWN = true;
                        break;
                    case Keys.LShiftKey:
                        SHIFT_DOWN = true;
                        break;
                    case Keys.LMenu:
                        ALT_DOWN = true;
                        break;
                    case Keys.LWin:
                        WIN_DOWN = true;
                        break;
                    default:
                        Debug.WriteLine($"{key} ctrl: {CONTROL_DOWN}, shift: {SHIFT_DOWN}, alt: {ALT_DOWN}, win: {WIN_DOWN}");
                        foreach (var shortcut in Shortcuts)
                        {
                            if (shortcut.Key == key && shortcut.Shift == SHIFT_DOWN && shortcut.Alt == ALT_DOWN && shortcut.Control == CONTROL_DOWN && shortcut.Win == WIN_DOWN)
                            {
                                shortcut.Activate();
                                return (IntPtr)(-1);
                            }
                        }
                        break;
                }

            }
            else if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)) 
            {
                //var key = (Keys)Marshal.ReadInt32(lParam);
                var keyboardHookStruct = (KeyboardHookStruct)Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));
                var key = (Keys)keyboardHookStruct.vkCode;
                //ALT_DOWN = keyboardHookStruct.flags != 0;
                switch (key)
                {
                    case Keys.LControlKey:
                        CONTROL_DOWN = false;
                        break;
                    case Keys.LShiftKey:
                        SHIFT_DOWN = false;
                        break;
                    case Keys.LMenu:
                        ALT_DOWN = false;
                        break;
                    case Keys.LWin:
                        WIN_DOWN = false;
                        break;
                    default:
                        break;
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam); //Call the next hook
        }

        public class Shortcut
        {
            public Shortcut(bool shift, bool control, bool alt, bool win, Keys key, int desktopId)
            {
                Shift = shift;
                Control = control;
                Alt = alt;
                Win = win;
                Key = key;
                EventArgs = new ShortcutEventArgs
                {
                    DesktopId = desktopId
                };
            }

            public bool Shift { get; }

            public bool Control { get; }

            public bool Alt { get; }

            public bool Win { get; }

            public Keys Key { get; }

            public ShortcutEventArgs EventArgs { get; set; }

            public event EventHandler<ShortcutEventArgs> Activated;
            public delegate void ShortcutActivatedEventHandler(object sender, System.EventArgs e);

            public void Activate()
            {
                Activated?.BeginInvoke(this, EventArgs, null, null);
            }
        }

        public class ShortcutEventArgs : EventArgs
        {
            public int DesktopId { get; set; }
        }
    }
}
