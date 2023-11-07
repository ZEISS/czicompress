// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models.Clipboard;

using Avalonia.Media.Imaging;

/// <summary>
/// Interface for utilities for interacting with the clipboard.
/// Unfortunately, the platform agnostic support in Avalonia is quite limited at this time,
/// so the idea here is to provide just enough functionality with platform specific implementations.
/// </summary>
public interface IClipboardHelper
{
    /// <summary>
    /// Puts the specified bitmap into the system clipboard. It is a simplistic implementation,
    /// the data is copied right away, meaning that the bitmap should not be large in size, a couple of
    /// kilobytes is fine. Also, the pixel format of the bitmap must be PixelFormat.Bgra8888, there is
    /// no conversion in place currently.
    /// </summary>
    /// <param name="bitmap">The bitmap to put on the clipboard.</param>
    void PutBitmapIntoClipboard(Bitmap bitmap);
}
