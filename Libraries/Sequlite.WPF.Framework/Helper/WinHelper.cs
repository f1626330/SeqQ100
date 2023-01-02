using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sequlite.WPF.Framework
{
    static public class WinHelper
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hwnd);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly UInt32 SWP_NOSIZE = 0x0001;
        static readonly UInt32 SWP_NOMOVE = 0x0002;
        static readonly UInt32 SWP_SHOWWINDOW = 0x0040;
        const int SW_MAXIMIZE = 3;
        private const int SWP_NOZORDER = 0x0004;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;

        static public bool SetWindowTopMost(IntPtr theWindowHandle)
        {
            //SetParent(parent, theWindowHandle);
            
            SetWindowPos(theWindowHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            //ShowWindow(theWindowHandle, 9);
            ////ShowWindow(theWindowHandle, SW_MAXIMIZE);
            //SetForegroundWindow(theWindowHandle);
            //SetActiveWindow(theWindowHandle);
            

            ////int style = GetWindowLong(theWindowHandle, GWL_STYLE);
            ////style = style & ~WS_CAPTION & ~WS_THICKFRAME;
            //SetWindowLong(theWindowHandle, GWL_STYLE, style);
            return true;
        }
    }
}
