// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.IO.Abstractions;

/// <summary>
/// A message containing information about the compression of a file.
/// This is a <see href="https://spencerfarley.com/2021/03/26/unions-in-csharp/">Union Type</see>.
/// </summary>
public record CompressorMessage
{
    private CompressorMessage()
    {
    }

    /// <summary>Message emitted when we have finished processing a file.</summary>
    /// <param name="InputFile">The file currently being processed.</param>
    /// <param name="SizeInput">The input size of the file.</param>
    /// <param name="SizeOutput">The output size of the file.</param>
    /// <param name="TimeElapsed">The time elapsed processing the file.</param>
    /// <param name="ErrorMessage">An optional error message.</param>
    public record FileFinished(IFileInfo InputFile, long SizeInput, long SizeOutput, TimeSpan? TimeElapsed,
            string? ErrorMessage)
        : CompressorMessage
    {
        public decimal SizeRatio { get; } = SizeInput != 0 ? (decimal)SizeOutput / SizeInput : 0;

        public long SizeDelta { get; } = SizeOutput - SizeInput;
    }

    /// <summary>
    /// Message emitted when we start processing a file.
    /// </summary>
    /// <param name="InputFile">The input file.</param>
    /// <param name="ProgressPercent">An observable that reports the progress in percent.</param>
    public record FileStarting(IFileInfo InputFile, IObservable<int> ProgressPercent)
        : CompressorMessage;
}
