using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace save_my_eyes.Services
{
    public sealed class WindowMinSize : IDisposable
    {
        private const uint WM_GETMINMAXINFO = 0x0024;
        private const uint WM_NCDESTROY = 0x0082;
        private static readonly UIntPtr SubclassId = new UIntPtr(0x5E80);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        private delegate IntPtr SubclassProc(
            IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, UIntPtr dwRefData);

        [DllImport("comctl32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

        [DllImport("comctl32.dll")]
        private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private readonly IntPtr _hwnd;
        private readonly int _minWidthDips;
        private readonly int _minHeightDips;

        private readonly SubclassProc _proc;
        private bool _disposed;

        public WindowMinSize(Window window, int minWidthDips, int minHeightDips)
        {
            _hwnd = WindowNative.GetWindowHandle(window);
            _minWidthDips = minWidthDips;
            _minHeightDips = minHeightDips;

            _proc = WindowProc;
            SetWindowSubclass(_hwnd, _proc, SubclassId, UIntPtr.Zero);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            RemoveWindowSubclass(_hwnd, _proc, SubclassId);
        }

        private IntPtr WindowProc(
            IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr idSubclass, UIntPtr refData)
        {
            try
            {
                if (msg == WM_GETMINMAXINFO && !_disposed)
                {
                    uint dpi = GetDpiForWindow(hWnd);
                    double scale = dpi == 0 ? 1.0 : dpi / 96.0;

                    MINMAXINFO info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    info.ptMinTrackSize.X = (int)Math.Round(_minWidthDips * scale);
                    info.ptMinTrackSize.Y = (int)Math.Round(_minHeightDips * scale);
                    Marshal.StructureToPtr(info, lParam, false);

                    return IntPtr.Zero;
                }

                if (msg == WM_NCDESTROY)
                {
                    RemoveWindowSubclass(hWnd, _proc, SubclassId);
                }
            }
            catch (Exception)
            {
            }

            return DefSubclassProc(hWnd, msg, wParam, lParam);
        }
    }
}
