// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

using DynamicData.Binding;

using netczicompress.Models;

/// <summary>
/// A view model for all in-progress single-file operations.
/// </summary>
public class CurrentTasksViewModel : ViewModelBase, ICurrentTasksViewModel, IObserver<CompressorMessage.FileStarting>
{
    private readonly ObservableCollectionExtended<ICompressionTaskViewModel> compressionTasks = new();
    private readonly IScheduler progressMonitoringScheduler;

    public CurrentTasksViewModel(IScheduler scheduler)
    {
        this.CompressionTasks = new ReadOnlyObservableCollection<ICompressionTaskViewModel>(this.compressionTasks);
        this.progressMonitoringScheduler = scheduler;
    }

    public ReadOnlyObservableCollection<ICompressionTaskViewModel> CompressionTasks { get; }

    public void OnCompleted()
    {
        this.compressionTasks.Clear();
    }

    public void OnError(Exception error)
    {
        this.OnCompleted();
    }

    public void OnNext(CompressorMessage.FileStarting value)
    {
        this.AddTask(value);
    }

    private void AddTask(CompressorMessage.FileStarting progress)
    {
        var vm = new CompressionTaskViewModel(
            progress.InputFile.FullName,
            progress.ProgressPercent.ObserveOn(this.progressMonitoringScheduler));
        vm.AddWhileNotCompleted(this.compressionTasks);
    }
}
