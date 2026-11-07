using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace save_my_eyes.Services
{
    public sealed class TrayService : IDisposable
    {
        private const uint WM_NULL = 0x0000;
        private const uint WM_NCDESTROY = 0x0082;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_TRAYICON = 0x8000 + 1;
        private const uint SC_MINIMIZE = 0xF020;

        private const uint NIM_ADD = 0;
        private const uint NIM_MODIFY = 1;
        private const uint NIM_DELETE = 2;
        private const uint NIF_MESSAGE = 0x1;
        private const uint NIF_ICON = 0x2;
        private const uint NIF_TIP = 0x4;

        private const uint MF_STRING = 0x0;
        private const uint MF_SEPARATOR = 0x800;
        private const uint TPM_RIGHTBUTTON = 0x2;
        private const uint TPM_NONOTIFY = 0x80;
        private const uint TPM_RETURNCMD = 0x100;

        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x10;
        private const int SM_CXSMICON = 49;
        private const int SM_CYSMICON = 50;
        private static readonly IntPtr IDI_APPLICATION = new IntPtr(32512);

        private const uint CmdOpen = 1;
        private const uint CmdToggle = 2;
        private const uint CmdExit = 3;

        private static readonly UIntPtr SubclassId = new UIntPtr(0x5E7E);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private delegate IntPtr SubclassProc(
            IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll", EntryPoint = "LoadIconW")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", EntryPoint = "AppendMenuW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private readonly Window _window;
        private readonly IntPtr _hwnd;

        private readonly SubclassProc _subclassProc;

        private IntPtr _hIcon;
        private bool _ownsIcon;
        private bool _iconVisible;
        private bool _hiding;
        private bool _disposed;
        private string _tooltip = "Save my Eyes";

        public Func<bool>? ShouldHideOnMinimize { get; set; }

        public string ToggleLabel { get; set; } = "Play";

        public event EventHandler? ToggleRequested;

        public event EventHandler? ExitRequested;

        public TrayService(Window window)
        {
            _window = window;
            _hwnd = WindowNative.GetWindowHandle(window);

            _subclassProc = WindowProc;
            SetWindowSubclass(_hwnd, _subclassProc, SubclassId, UIntPtr.Zero);

            _window.AppWindow.Changed += OnAppWindowChanged;
        }

        public void HideToTray()
        {
            if (_disposed || _hiding)
            {
                return;
            }

            _hiding = true;
            try
            {
                AddOrUpdateIcon();
                _window.AppWindow.Hide();
            }
            finally
            {
                _hiding = false;
            }
        }

        public void RestoreFromTray()
        {
            try
            {
                if (_window.AppWindow.Presenter is OverlappedPresenter presenter &&
                    presenter.State == OverlappedPresenterState.Minimized)
                {
                    presenter.Restore();
                }

                _window.AppWindow.Show();
                _window.Activate();
                SetForegroundWindow(_hwnd);
            }
            catch (Exception)
            {
            }

            RemoveIcon();
        }

        public void UpdateTooltip(string text)
        {
            _tooltip = text;

            if (_iconVisible)
            {
                AddOrUpdateIcon();
            }
        }

        public void RemoveIcon()
        {
            if (!_iconVisible)
            {
                return;
            }

            NOTIFYICONDATA data = NewData();
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _iconVisible = false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _window.AppWindow.Changed -= OnAppWindowChanged;
            }
            catch (Exception)
            {
            }

            RemoveIcon();
            RemoveWindowSubclass(_hwnd, _subclassProc, SubclassId);

            if (_ownsIcon && _hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
            }

            _hIcon = IntPtr.Zero;
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (_disposed)
            {
                return;
            }

            if (args.DidVisibilityChange && sender.IsVisible)
            {
                RemoveIcon();
                return;
            }

            if (sender.IsVisible &&
                sender.Presenter is OverlappedPresenter presenter &&
                presenter.State == OverlappedPresenterState.Minimized &&
                ShouldHideOnMinimize?.Invoke() == true)
            {
                HideToTray();
            }
        }

        private IntPtr WindowProc(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr idSubclass, UIntPtr refData)
        {
            try
            {
                switch (msg)
                {
                    case WM_SYSCOMMAND:
                        if (((uint)(wParam.ToInt64() & 0xFFF0)) == SC_MINIMIZE &&
                            ShouldHideOnMinimize?.Invoke() == true)
                        {
                            HideToTray();
                            return IntPtr.Zero;
                        }
                        break;

                    case WM_TRAYICON:
                        uint mouseMessage = (uint)(lParam.ToInt64() & 0xFFFF);

                        if (mouseMessage == WM_LBUTTONUP)
                        {
                            RestoreFromTray();
                        }
                        else if (mouseMessage == WM_RBUTTONUP)
                        {
                            ShowMenu();
                        }

                        return IntPtr.Zero;

                    case WM_NCDESTROY:
                        RemoveIcon();
                        RemoveWindowSubclass(hWnd, _subclassProc, SubclassId);
                        break;
                }
            }
            catch (Exception)
            {
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }

        private void ShowMenu()
        {
            GetCursorPos(out POINT point);

            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                AppendMenu(menu, MF_STRING, new UIntPtr(CmdOpen), "Open");
                AppendMenu(menu, MF_STRING, new UIntPtr(CmdToggle), ToggleLabel);
                AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                AppendMenu(menu, MF_STRING, new UIntPtr(CmdExit), "Exit");

                SetForegroundWindow(_hwnd);

                uint command = TrackPopupMenu(
                    menu,
                    TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY,
                    point.X,
                    point.Y,
                    0,
                    _hwnd,
                    IntPtr.Zero);

                PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                switch (command)
                {
                    case CmdOpen:
                        RestoreFromTray();
                        break;
                    case CmdToggle:
                        ToggleRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case CmdExit:
                        ExitRequested?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
            finally
            {
                DestroyMenu(menu);
            }
        }

        private void AddOrUpdateIcon()
        {
            EnsureIconLoaded();

            NOTIFYICONDATA data = NewData();
            data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            data.uCallbackMessage = WM_TRAYICON;
            data.hIcon = _hIcon;
            data.szTip = _tooltip.Length > 127 ? _tooltip.Substring(0, 127) : _tooltip;

            if (_iconVisible)
            {
                Shell_NotifyIcon(NIM_MODIFY, ref data);
            }
            else if (Shell_NotifyIcon(NIM_ADD, ref data))
            {
                _iconVisible = true;
            }
        }

        private NOTIFYICONDATA NewData()
        {
            return new NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                szTip = string.Empty,
                szInfo = string.Empty,
                szInfoTitle = string.Empty
            };
        }

        private void EnsureIconLoaded()
        {
            if (_hIcon != IntPtr.Zero)
            {
                return;
            }

            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");

            if (File.Exists(path))
            {
                _hIcon = LoadImage(
                    IntPtr.Zero,
                    path,
                    IMAGE_ICON,
                    GetSystemMetrics(SM_CXSMICON),
                    GetSystemMetrics(SM_CYSMICON),
                    LR_LOADFROMFILE);

                _ownsIcon = _hIcon != IntPtr.Zero;
            }

            if (_hIcon == IntPtr.Zero)
            {
                _hIcon = LoadIcon(IntPtr.Zero, IDI_APPLICATION);
                _ownsIcon = false;
            }
        }
    }
}
