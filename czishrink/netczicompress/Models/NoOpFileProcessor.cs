// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.Threading;

/// <summary>
/// An <see cref="IFileProcessor"/> that does not do anything.
/// </summary>
public sealed class NoOpFileProcessor : IFileProcessor
{
    public bool NeedsExistingOutputDirectory => false;

    public void ProcessFile(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token)
    {
        progressReport(100);
    }

    public void Dispose()
    {
    }
}
