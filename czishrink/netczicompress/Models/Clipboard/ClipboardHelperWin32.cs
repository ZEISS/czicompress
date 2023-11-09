// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models.Clipboard;

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

/// <summary>
/// An implementation of the IClipboardHelper interface based on Win32-API.
/// Obviously, this code is only operational if running on Win32 - which is not
/// checked here (i.e. it is assumed that platform specific dispatching is taking
/// place before using this code).
/// </summary>
internal partial class ClipboardHelperWin32 : IClipboardHelper
{
    public void PutBitmapIntoClipboard(Bitmap bitmap)
    {
        if (bitmap == null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        if (bitmap.Format != PixelFormat.Bgra8888)
        {
            throw new ArgumentException("Bitmap is expected to be in PixelFormat.Bgra8888, conversion is not implemented");
        }

        nint hBitmap = 0;
        nint screenDC = 0;
        nint sourceDC = 0;
        nint destDC = 0;
        nint compatibleBitmap = 0;
        bool clipboardOpened = false;

        try
        {
            hBitmap = ClipboardHelperWin32.CreateHBitmapFromAvalonBitmap(bitmap);

            screenDC = Win32UnmanagedMethods.GetDC(IntPtr.Zero);
            if (screenDC == 0)
            {
                throw new Exception("Error creating screenDC");
            }

            sourceDC = Win32UnmanagedMethods.CreateCompatibleDC(screenDC);
            if (sourceDC == 0)
            {
                throw new Exception("Error creating sourceDC");
            }

            Win32UnmanagedMethods.SelectObject(sourceDC, hBitmap);

            destDC = Win32UnmanagedMethods.CreateCompatibleDC(screenDC);
            if (destDC == 0)
            {
                throw new Exception("Error creating destDC");
            }

            compatibleBitmap = Win32UnmanagedMethods.CreateCompatibleBitmap(screenDC, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            if (compatibleBitmap == 0)
            {
                throw new Exception("Error creating compatibleBitmap");
            }

            Win32UnmanagedMethods.SelectObject(destDC, compatibleBitmap);
            Win32UnmanagedMethods.BitBlt(
                                    destDC,
                                    0,
                                    0,
                                    bitmap.PixelSize.Width,
                                    bitmap.PixelSize.Height,
                                    sourceDC,
                                    0,
                                    0,
                                    0x00CC0020); // SRCCOPY

            if (!Win32UnmanagedMethods.OpenClipboard(IntPtr.Zero))
            {
                throw new Exception("Error opening the clipboard");
            }

            clipboardOpened = true;

            Win32UnmanagedMethods.EmptyClipboard();
            Win32UnmanagedMethods.SetClipboardData(Win32UnmanagedMethods.ClipboardFormat.CF_BITMAP, compatibleBitmap);
        }
        finally
        {
            if (clipboardOpened)
            {
                Win32UnmanagedMethods.CloseClipboard();
            }

            bool success = false;
            if (hBitmap != 0)
            {
                success = Win32UnmanagedMethods.DeleteObject(hBitmap);
            }

            if (compatibleBitmap != 0)
            {
                success = Win32UnmanagedMethods.DeleteObject(compatibleBitmap);
            }

            if (sourceDC != 0)
            {
                success = Win32UnmanagedMethods.DeleteDC(sourceDC);
            }

            if (screenDC != 0)
            {
                success = Win32UnmanagedMethods.ReleaseDC(IntPtr.Zero, screenDC);
            }

            if (destDC != 0)
            {
                success = Win32UnmanagedMethods.DeleteDC(destDC);
            }

            _ = success;
        }
    }

    private static unsafe nint CreateHBitmapFromAvalonBitmap(Bitmap bitmap)
    {
        if (bitmap.Format != PixelFormat.Bgra8888)
        {
            throw new ArgumentException("Bitmap is expected to be in PixelFormat.Bgra8888, conversion is not implemented");
        }

        byte[] buffer = new byte[bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4];
        fixed (byte* p = &buffer[0])
        {
            // copy the content of the Avalonia-bitmap into the buffer
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), new IntPtr(p), buffer.Length, bitmap.PixelSize.Width * 4);

            // now, we allocate an "HBITMAP"-object and copy the data into it
            Win32UnmanagedMethods.BITMAPINFO bmi = default;
            bmi.bmiHeader.Init();
            bmi.bmiHeader.biWidth = bitmap.PixelSize.Width;
            bmi.bmiHeader.biHeight = -bitmap.PixelSize.Height; // top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = Win32UnmanagedMethods.BitmapCompressionMode.BI_RGB;
            IntPtr ptr = IntPtr.Zero;
            nint hBitmap = Win32UnmanagedMethods.CreateDIBSection(
                IntPtr.Zero,
                ref bmi,
                0,  /*DIB_RGB_COLORS*/
                out ptr,
                IntPtr.Zero,
                0);
            if (hBitmap == 0)
            {
                throw new Exception("CreateDIBSection failed.");
            }

            int r = Win32UnmanagedMethods.SetDIBits(
                 IntPtr.Zero,
                 hBitmap,
                 0,
                 (uint)bitmap.PixelSize.Height,
                 new IntPtr(p),
                 ref bmi,
                 0);    /*DIB_RGB_COLORS*/

            if (r == 0)
            {
                Win32UnmanagedMethods.DeleteObject(hBitmap);
                throw new Exception("SetDIBits failed.");
            }

            return hBitmap;
        }
    }
}

/// <content>
/// This part contains the PInvoke definitions we need in order to "put the bitmap into the clipboard"
/// with the Win32-API.
/// </content>
internal partial class ClipboardHelperWin32
{
    private class Win32UnmanagedMethods
    {
        public enum BitmapCompressionMode : uint
        {
            BI_RGB = 0,
            BI_RLE8 = 1,
            BI_RLE4 = 2,
            BI_BITFIELDS = 3,
            BI_JPEG = 4,
            BI_PNG = 5,
        }

        public enum ClipboardFormat
        {
            /// <summary>
            /// Text format. Each line ends with a carriage return/linefeed (CR-LF) combination. A null character signals the end of the data. Use this format for ANSI text.
            /// </summary>
            CF_TEXT = 1,

            /// <summary>
            /// A handle to a bitmap.
            /// </summary>
            CF_BITMAP = 2,

            /// <summary>
            /// A memory object containing a BITMAPINFO structure followed by the bitmap bits.
            /// </summary>
            CF_DIB = 3,

            /// <summary>
            /// Unicode text format. Each line ends with a carriage return/linefeed (CR-LF) combination. A null character signals the end of the data.
            /// </summary>
            CF_UNICODETEXT = 13,

            /// <summary>
            /// A handle to type HDROP that identifies a list of files.
            /// </summary>
            CF_HDROP = 15,
        }

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BITMAPINFO pbmi, uint pila, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        [DllImport("gdi32.dll")]
        public static extern int SetDIBits(IntPtr hdc, nint hbm, uint start, uint cLines, IntPtr lpBits, [In] ref BITMAPINFO pbmi, uint colorUse);

        [DllImport("user32.dll", ExactSpelling = true)]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
        public static extern bool DeleteDC([In] IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool BitBlt(
                                        IntPtr hdc,
                                        int x,
                                        int y,
                                        int cx,
                                        int cy,
                                        IntPtr hdcSrc,
                                        int x1,
                                        int y1,
                                        uint rop);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool OpenClipboard(IntPtr hWndOwner);

        [DllImport("user32.dll")]
        public static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        public static extern IntPtr SetClipboardData(ClipboardFormat uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseClipboard();

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public BitmapCompressionMode biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter

            public void Init()
            {
                this.biSize = (uint)Marshal.SizeOf(this);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RGBQUAD
        {
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
            /// <summary>
            /// A BITMAPINFOHEADER structure that contains information about the dimensions of color format.
            /// </summary>
            public BITMAPINFOHEADER bmiHeader;

            /// <summary>
            /// An array of RGBQUAD. The elements of the array that make up the color table.
            /// </summary>
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.Struct)]
            public RGBQUAD[] bmiColors;
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
        }
    }
}
