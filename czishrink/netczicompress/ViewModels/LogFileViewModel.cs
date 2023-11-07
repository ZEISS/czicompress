// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.IO.Abstractions;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;

using netczicompress.Models;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

/// <summary>
/// View model that deals with the log file.
/// </summary>
public class LogFileViewModel : ViewModelBase, ILogFileViewModel, IFolderCompressorRunObserver<CompressorMessage.FileFinished>
{
    private readonly ILoggingStrategy loggingStrategy;
    private readonly IScheduler guiScheduler;

    public LogFileViewModel(ILoggingStrategy loggingStrategy, IScheduler guiScheduler)
    {
        this.OpenLogFileCommand = ReactiveCommand.CreateFromTask(
            this.OpenLogFile,
            this.WhenAnyValue(x => x.LogFileOfLastRun).Select(x => x != null));
        this.loggingStrategy = loggingStrategy;
        this.guiScheduler = guiScheduler;
    }

    [Reactive]
    public IFileInfo? LogFileOfLastRun { get; private set; }

    public ICommand OpenLogFileCommand { get; }

    public void ObserveRun(FolderCompressorParameters runParameters, IObservable<CompressorMessage.FileFinished> runMessages)
    {
        var logFile = this.loggingStrategy.CreateLogFile(runParameters);
        this.LogFileOfLastRun = null;
        var logger = this.loggingStrategy.CreateLogger(logFile.CreateText());

        void PublishLogFile() =>
            this.guiScheduler.Schedule(() => this.LogFileOfLastRun = logFile);

        var noErrors = runMessages.CompleteOnError();
        _ = noErrors.ObserveOn(this.loggingStrategy.LoggerScheduler).Subscribe(logger);
        _ = noErrors.Subscribe(onNext: _ => { }, onCompleted: PublishLogFile);
    }

    private async Task OpenLogFile()
    {
        if (this.LogFileOfLastRun == null)
        {
            return;
        }

        try
        {
            this.LogFileOfLastRun.Refresh();
            if (this.LogFileOfLastRun.Exists)
            {
                await this.loggingStrategy.OpenLogFile(this.LogFileOfLastRun);
            }
            else
            {
                this.LogFileOfLastRun = null;
            }
        }
        catch
        {
            this.LogFileOfLastRun = null;
        }
    }
}
