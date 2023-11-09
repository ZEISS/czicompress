// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Threading;

/// <summary>
/// Decorates an <see cref="IFolderCompressor"/> with configurable output observation.
/// </summary>
public class FolderCompressorDecorator : IFolderCompressor
{
    /// <summary>
    /// Gets the core instance to wrap.
    /// </summary>
    public required IFolderCompressor Core { get; init; }

    /// <summary>
    /// Gets the action that is used to start observing the output of the folder compressor.</summary>
    /// <remarks>
    /// The observable that is passed to <see name="ObserveRun"/> will terminate if the
    /// returned <see cref="IFolderCompressorRun"/> is started.
    /// In that case, their is no need to explicitly dispose subscriptions.
    /// </remarks>
    public required Action<FolderCompressorParameters, IObservable<CompressorMessage>> ObserveRun { get; init; }

    public IFolderCompressorRun PrepareNewRun(FolderCompressorParameters parameters, CancellationToken token)
    {
        var result = this.Core.PrepareNewRun(parameters, token);
        this.ObserveRun(parameters, result.Output);
        return result;
    }
}