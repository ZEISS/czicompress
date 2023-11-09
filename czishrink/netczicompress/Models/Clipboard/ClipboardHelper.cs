// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models.Clipboard;

using System.Runtime.InteropServices;

internal static class ClipboardHelper
{
    private static readonly IClipboardHelper? InstanceValue;

    static ClipboardHelper()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            InstanceValue = new ClipboardHelperWin32();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            InstanceValue = new ClipboardHelperLinux();
        }
    }

    public static IClipboardHelper? Instance
    {
        get
        {
            return InstanceValue;
        }
    }
}
