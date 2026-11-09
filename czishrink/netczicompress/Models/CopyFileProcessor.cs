// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
//
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Contributor: Amin Khosrozadeh, PhD.
// Affiliation: Prof. Vassiliki Nikoletopoulou lab, DNF, UNIL.

namespace netczicompress.Models;

using System.IO.Abstractions;

/// <summary>
/// Copies a non-CZI file unchanged, with progress reporting and cancellation.
/// </summary>
public sealed class CopyFileProcessor(IFileSystem fileSystem) : IFileProcessor
{
    public bool NeedsExistingOutputDirectory => true;

    public void ProcessFile(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var input = fileSystem.FileStream.New(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        long inputLength = input.Length;
        using (var output = fileSystem.FileStream.New(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
        {
            byte[] buffer = new byte[1024 * 1024];
            long bytesCopied = 0;
            int bytesRead;
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                output.Write(buffer, 0, bytesRead);
                bytesCopied += bytesRead;
                progressReport(inputLength == 0 ? 100 : (int)(100.0 * bytesCopied / inputLength));
            }
        }

        token.ThrowIfCancellationRequested();
        fileSystem.File.SetLastWriteTimeUtc(outputPath, fileSystem.File.GetLastWriteTimeUtc(inputPath));
        progressReport(100);
        token.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
    }
}
