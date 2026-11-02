// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.IO.Abstractions;
using System.Security.Cryptography;

/// <summary>
/// Copies an original file unchanged, with progress reporting and cancellation.
/// </summary>
public sealed class CopyFileProcessor(IFileSystem fileSystem) : IFileProcessor, IProcessingDetailsSource
{
    public bool NeedsExistingOutputDirectory => true;

    public ProcessingDetails? LastDetails { get; private set; }

    public void ProcessFile(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        this.LastDetails = null;
        using var sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var input = fileSystem.FileStream.New(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using (var output = fileSystem.FileStream.New(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            byte[] buffer = new byte[1024 * 1024];
            long bytesCopied = 0;
            int bytesRead;
            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                output.Write(buffer, 0, bytesRead);
                sourceHash.AppendData(buffer, 0, bytesRead);
                bytesCopied += bytesRead;
                progressReport(input.Length == 0 ? 100 : (int)(100.0 * bytesCopied / input.Length));
            }
        }

        token.ThrowIfCancellationRequested();
        var inputSha = Convert.ToHexString(sourceHash.GetHashAndReset());
        string outputSha;
        using (var copied = fileSystem.FileStream.New(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            outputSha = FileIntegrity.ComputeSha256(copied, token);
        }

        if (inputSha != outputSha)
        {
            throw new IOException("Copied file failed SHA-256 verification.");
        }

        fileSystem.File.SetLastWriteTimeUtc(outputPath, fileSystem.File.GetLastWriteTimeUtc(inputPath));
        this.LastDetails = new ProcessingDetails(
            Path.GetExtension(inputPath).TrimStart('.').ToUpperInvariant(),
            "Copy",
            "Byte copy",
            "Copied unchanged",
            "SHA-256 byte-for-byte copy verified",
            true,
            inputSha,
            outputSha);
        progressReport(100);
        token.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
    }
}
