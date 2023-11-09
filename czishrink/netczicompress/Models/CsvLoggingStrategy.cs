// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Concurrency;

/// <summary>
/// A logging strategy that writes data to a CSV log file.
/// </summary>
public class CsvLoggingStrategy : ILoggingStrategy
{
    private readonly IFileLauncher fileLauncher;

    public CsvLoggingStrategy(IFileLauncher fileLauncher)
    {
        this.fileLauncher = fileLauncher;
    }

    /// <summary>
    /// Gets the scheduler to use for logging.
    /// </summary>
    public IScheduler LoggerScheduler { get; init; } = DefaultScheduler.Instance;

    public IFileInfo CreateLogFile(FolderCompressorParameters run)
    {
        var outDir = run.OutputDir;
        var fs = outDir.FileSystem;
        IFileInfo result;
        do
        {
            var outFileName = fs.Path.Combine(
                outDir.FullName,
                $"CziShrink_{this.LoggerScheduler.Now.ToLocalTime():yyyyMMdd'T'HHmmss}_{run.Mode}.csv");

            result = fs.FileInfo.New(outFileName);
        }
        while (result.Exists);

        return result;
    }

    public IObserver<CompressorMessage.FileFinished> CreateLogger(TextWriter writer)
    {
        return new CsvLogFileWriter(writer);
    }

    public Task OpenLogFile(IFileInfo logFile)
    {
        return this.fileLauncher.Launch(logFile.FullName);
    }
}
