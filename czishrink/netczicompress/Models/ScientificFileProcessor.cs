// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Runs portable scientific codecs in an isolated process and bounds memory use.
/// </summary>
public sealed class ScientificFileProcessor(CompressionMode mode, CompressionLevel level, string? toolsDirectory = null) : IFileProcessor, IProcessingDetailsSource
{
    private static readonly SemaphoreSlim WorkerSlot = new(1, 1);

    public bool NeedsExistingOutputDirectory => true;

    public ProcessingDetails? LastDetails { get; private set; }

    public void ProcessFile(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token)
    {
        WorkerSlot.Wait(token);
        try
        {
            this.LastDetails = null;
            this.RunWorker(inputPath, outputPath, progressReport, token);
        }
        catch (Win32Exception ex)
        {
            throw new IOException("The scientific processing runtime could not start. Keep the entire portable app folder together.", ex);
        }
        finally
        {
            WorkerSlot.Release();
        }
    }

    public void Dispose()
    {
    }

    private void RunWorker(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token)
    {
        var root = toolsDirectory ?? Environment.GetEnvironmentVariable("CZISHRINK_FORMAT_TOOLS")
            ?? Path.Combine(AppContext.BaseDirectory, "FormatTools");
        var python = Path.Combine(root, "python", OperatingSystem.IsWindows() ? "python.exe" : "bin/python3");
        var script = Path.Combine(root, "scientific_worker.py");
        if (!File.Exists(python) || !File.Exists(script))
        {
            throw new IOException("Scientific format tools are missing. Keep the FormatTools folder beside CziShrink.exe.");
        }

        var start = new ProcessStartInfo(python)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var arguments = new[]
        {
            "-B", script, "--input", inputPath, "--output", outputPath,
            "--mode", mode.ToString(), "--level", Math.Clamp(level.Value, 1, 9).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["PYTHONUTF8"] = "1";
        var scratch = Path.Combine(Path.GetDirectoryName(outputPath)!, $".czishrink-tools-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scratch);
        using var cleanup = System.Reactive.Disposables.Disposable.Create(() => Directory.Delete(scratch, recursive: true));
        start.Environment["CZISHRINK_WORK_DIRECTORY"] = scratch;
        using var process = Process.Start(start) ?? throw new IOException("Scientific format worker did not start.");
        using var cancel = token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The worker may exit while cancellation is being delivered.
            }
        });
        var stderr = process.StandardError.ReadToEndAsync();
        string? error = null;
        bool receivedResult = false;
        string? line;
        while ((line = process.StandardOutput.ReadLine()) != null)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("progress", out var progress))
                {
                    progressReport(progress.GetInt32());
                }

                if (document.RootElement.TryGetProperty("error", out var failure))
                {
                    error = failure.GetString();
                }

                if (document.RootElement.TryGetProperty("result", out var result))
                {
                    receivedResult = true;
                    var copied = result.TryGetProperty("unchanged", out var unchanged) && unchanged.GetBoolean();
                    string? Read(string key) => result.TryGetProperty(key, out var value) ? value.GetString() : null;
                    var format = Path.GetExtension(inputPath).TrimStart('.').ToUpperInvariant();
                    this.LastDetails = new ProcessingDetails(
                        format,
                        mode.ToString(),
                        copied ? "Byte copy" : Read("compression") ?? "Unknown",
                        copied ? Read("reason") ?? "Copied unchanged" : mode == CompressionMode.Decompress ? "Decompressed" : "Compressed",
                        copied ? "SHA-256 byte-for-byte copy verified" : format is "H5" or "HDF5" ? "h5diff, reference targets/regions and fixed-width bytes verified" : "Decoded pixels and metadata verified",
                        result.TryGetProperty("container_byte_identical", out var identical) ? identical.GetBoolean() : null,
                        Read("input_sha256"),
                        Read("output_sha256"));
                }
            }
            catch (JsonException)
            {
                // Third-party libraries may print informational lines.
            }
        }

        process.WaitForExit();
        var diagnostic = stderr.GetAwaiter().GetResult();
        token.ThrowIfCancellationRequested();
        if (process.ExitCode != 0 || !receivedResult || !File.Exists(outputPath))
        {
            throw new IOException(error ?? $"Scientific processing failed: {diagnostic}");
        }
    }
}
