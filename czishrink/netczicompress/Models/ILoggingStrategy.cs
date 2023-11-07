// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Concurrency;

/// <summary>
/// A text-file-based logging strategy.
/// </summary>
public interface ILoggingStrategy
{
    public IScheduler LoggerScheduler { get; }

    public IFileInfo CreateLogFile(FolderCompressorParameters run);

    public IObserver<CompressorMessage.FileFinished> CreateLogger(TextWriter writer);

    public Task OpenLogFile(IFileInfo file);
}
