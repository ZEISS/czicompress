// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Threading;

/// <summary>
/// An object that can process a single file.
/// </summary>
public interface IFileProcessor : IDisposable
{
    /// <summary>
    /// Gets a value indicating whether the file processor needs the output directory to exist when <see cref="ProcessFile"/>
    /// is called.
    /// </summary>
    bool NeedsExistingOutputDirectory { get; }

    /// <summary>
    /// Processes a single input file to a single output file.
    /// </summary>
    /// <param name="inputPath">The absolute path of the input file.</param>
    /// <param name="outputPath">The absolute path of the output file.</param>
    /// <param name="progressReport">The progress delegate.</param>
    /// <param name="token">The cancellation token.</param>
    void ProcessFile(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token);
}
