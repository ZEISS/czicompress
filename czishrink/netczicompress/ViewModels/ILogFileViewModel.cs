// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.IO.Abstractions;
using System.Windows.Input;

using netczicompress.Models;

/// <summary>
/// A view model that produces and opens log files for folder compressor runs.
/// </summary>
public interface ILogFileViewModel
{
    IFileInfo? LogFileOfLastRun { get; }

    ICommand OpenLogFileCommand { get; }

    void ObserveRun(FolderCompressorParameters runParameters, IObservable<CompressorMessage.FileFinished> runMessages);
}
