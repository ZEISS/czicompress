// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.Threading;

/// <summary>
/// An object that can compress files in an input folder to an output folder.
/// </summary>
public interface IFolderCompressor
{
    /// <summary>
    /// Prepare a new compressor run with the specified parameters.
    /// </summary>
    /// <param name="parameters">Parameters of the compression operation that is to be performed.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A new compressor run.</returns>
    /// <remarks>
    /// The actual compression is started when the <see cref="IFolderCompressorRun.Start"/> method is called on the returned object.
    /// </remarks>
    IFolderCompressorRun PrepareNewRun(FolderCompressorParameters parameters, CancellationToken token);
}
