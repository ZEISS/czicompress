// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later
//
// Contributor: Amin Khosrozadeh, PhD.
// Affiliation: Prof. Vassiliki Nikoletopoulou lab, DNF, UNIL.

namespace netczicompress.Models;

/// <summary>
/// Routes enabled scientific formats to their codecs, retaining the native CZI engine.
/// </summary>
public sealed class MultiFormatFileProcessor(CompressionMode mode, ProcessingOptions options) : IFileProcessor, IProcessingDetailsSource
{
    private readonly ScientificFileProcessor scientific = new(mode, options.CompressionLevel);
    private IFileProcessor? czi;

    public bool NeedsExistingOutputDirectory => true;

    public ProcessingDetails? LastDetails { get; private set; }

    public void ProcessFile(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token)
    {
        this.LastDetails = null;
        if (string.Equals(Path.GetExtension(inputPath), ".czi", StringComparison.OrdinalIgnoreCase))
        {
            this.czi ??= PInvokeFileProcessor.Create(mode, options);
            this.czi.ProcessFile(inputPath, outputPath, progressReport, token);
            using var input = File.OpenRead(inputPath);
            using var output = File.OpenRead(outputPath);
            var inputSha = FileIntegrity.ComputeSha256(input, token);
            var outputSha = FileIntegrity.ComputeSha256(output, token);
            this.LastDetails = new ProcessingDetails(
                "CZI",
                mode.ToString(),
                "Native CZI engine (" + mode + ")",
                mode == CompressionMode.Decompress ? "Decompressed" : "Processed by CZI engine",
                "CZI lossless codec; decoded pixels not independently checked per file",
                inputSha == outputSha,
                inputSha,
                outputSha);
        }
        else
        {
            this.scientific.ProcessFile(inputPath, outputPath, progressReport, token);
            this.LastDetails = this.scientific.LastDetails;
        }
    }

    public void Dispose()
    {
        this.czi?.Dispose();
        this.scientific.Dispose();
    }
}
