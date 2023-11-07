// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models.Clipboard;

using System.Diagnostics;
using Avalonia.Media.Imaging;

/// <summary>
/// This is an implementation of the IClipboardHelper interface for the X-Windows system. We use the
/// utility "xclip" as an external program which we launch and pass to it the bitmap as a PNG.
/// </summary>
internal class ClipboardHelperLinux : IClipboardHelper
{
    public void PutBitmapIntoClipboard(Bitmap bitmap)
    {
        if (bitmap == null)
        {
            throw new ArgumentNullException(nameof(bitmap));
        }

        using (var memoryStream = new MemoryStream())
        {
            bitmap.Save(memoryStream);
            memoryStream.Position = 0;
            ClipboardHelperLinux.SetImageToClipboard(memoryStream, "image/png");
        }
    }

    private static void SetImageToClipboard(Stream imageData, string mimeType)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "xclip",
            ArgumentList =
            {
                "-selection", "clipboard",
                "-t", mimeType,
                "-i",
            },
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();

            // the png is now written to stdout
            imageData.CopyTo(process.StandardInput.BaseStream);

            // then we close stdout, and xclip will start operation
            process.StandardInput.Close();
            process.WaitForExit();
        }
    }
}
