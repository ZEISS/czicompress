// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.IO.Abstractions;

/// <summary>
/// A handler which deals with the case when a file processing fails.
/// </summary>
public class FileProcessingFailedHandler
{
    public const string SuccessfulCopyMessage = "The file was copied as-is to the output folder.";
    public const string FailedCopyMessage = "The file could not be copied to the output folder.";

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
        if (copyFailedFile)
        {
            return CopyFile(inFile, outFile, tempOutFile, reportProgress, cancellationToken)
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

    private static bool CopyFile(
        IFileInfo inFile,
        IFileInfo outFile,
        IFileInfo tempOutFile,
        ReportProgress reportProgress,
        CancellationToken cancellationToken)
    {
        try
        {
            CopyWithProgress(inFile.FullName, tempOutFile.FullName, reportProgress, cancellationToken);
            tempOutFile.Refresh();
            tempOutFile.MoveTo(outFile.FullName);
            return true;
        }
        catch (Exception)
        {
            // Remove partially written file
            _ = TryDelete(outFile);
            _ = TryDelete(tempOutFile);
            return false;
        }
    }

    private static void CopyWithProgress(
        string inPath,
        string outPath,
        ReportProgress reportProgress,
        CancellationToken cancellationToken)
    {
        long totalBytesWritten = 0;
        int bufferLength = 1024 * 1024;
        byte[] buffer = new byte[bufferLength];
        using var inStream = new FileStream(inPath, FileMode.Open);
        using var tempOutStream = new FileStream(outPath, FileMode.CreateNew);

        int bytesRead = inStream.Read(buffer, 0, bufferLength);

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            tempOutStream.Write(buffer, 0, bytesRead);
            totalBytesWritten += bytesRead;

            int progress = inStream.Length != 0 ? (int)(((totalBytesWritten * 1.0) / inStream.Length) * 100) : 100;
            reportProgress.Invoke(progress);

            bytesRead = inStream.Read(buffer, 0, bufferLength);
        }
        while (bytesRead > 0);
    }
}
