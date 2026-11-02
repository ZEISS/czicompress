// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.IO.Abstractions;

/// <summary>
/// A handler which deals with the case when a file processing fails.
/// </summary>
public class FileProcessingFailedHandler : IProcessingDetailsSource
{
    public const string SuccessfulCopyMessage = "The file was copied as-is to the output folder.";
    public const string FailedCopyMessage = "The file could not be copied to the output folder.";

    private readonly AsyncLocal<ProcessingDetails?> lastDetails = new();

    public ProcessingDetails? LastDetails => this.lastDetails.Value;

    /// <summary>
    /// Handles the case when a file processing has failed.
    /// </summary>
    /// <param name="inFile">The input file.</param>
    /// <param name="outFile">The output file.</param>
    /// <param name="tempOutFile">The temporary output file.</param>
    /// <param name="reportProgress">The delegate to report progress.</param>
    /// <param name="innerException">The inner exception, that has occurred.</param>
    /// <param name="copyFailedFile">A value indicating whether the failed file should by copied.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A message which described how the failed file processing was handled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public string FileProcessingFailed(
        IFileInfo inFile,
        IFileInfo outFile,
        IFileInfo tempOutFile,
        ReportProgress reportProgress,
        string innerException,
        bool copyFailedFile,
        CancellationToken cancellationToken)
    {
        this.lastDetails.Value = null;
        if (copyFailedFile)
        {
            return this.CopyFile(inFile, outFile, tempOutFile, reportProgress, cancellationToken)
                ? $"{innerException} {SuccessfulCopyMessage}"
                : $"{innerException} {FailedCopyMessage}";
        }

        return innerException;
    }

    private static bool TryDelete(IFileInfo fileInfo)
    {
        try
        {
            fileInfo.Delete();
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private bool CopyFile(
        IFileInfo inFile,
        IFileInfo outFile,
        IFileInfo tempOutFile,
        ReportProgress reportProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            using var copier = new CopyFileProcessor(new FileSystem());
            copier.ProcessFile(inFile.FullName, tempOutFile.FullName, reportProgress, cancellationToken);
            tempOutFile.Refresh();
            cancellationToken.ThrowIfCancellationRequested();
            tempOutFile.MoveTo(outFile.FullName);
            this.lastDetails.Value = copier.LastDetails! with { Action = "Copied original after processing failure" };
            return true;
        }
        catch (OperationCanceledException)
        {
            _ = TryDelete(tempOutFile);
            throw;
        }
        catch (Exception)
        {
            // Remove partially written file
            _ = TryDelete(tempOutFile);
            return false;
        }
    }
}
