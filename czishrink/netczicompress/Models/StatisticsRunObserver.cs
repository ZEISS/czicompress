// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

/// <summary>
/// An <see cref="IFolderCompressorRunObserver{TMessage}"/> that forwards <see cref="AggregateStatistics"/> to an observer of these.
/// </summary>
public class StatisticsRunObserver : IFolderCompressorRunObserver<CompressorMessage.FileFinished>
{
    private readonly IObserver<AggregateStatistics> statisticsObserver;
    private readonly TimeSpan samplingInterval;
    private readonly IScheduler scheduler;

    public StatisticsRunObserver(
        IObserver<AggregateStatistics> statisticsObserver,
        TimeSpan samplingInterval,
        IScheduler scheduler)
    {
        this.statisticsObserver = statisticsObserver;
        this.samplingInterval = samplingInterval;
        this.scheduler = scheduler;
    }

    public void ObserveRun(FolderCompressorParameters runParameters, IObservable<CompressorMessage.FileFinished> runMessages)
    {
        var start = this.scheduler.Now;

        var emptyStats = AggregateStatistics.Empty;
        this.statisticsObserver.OnNext(emptyStats);
        var cumulativeStatistics = runMessages
            .CompleteOnError()
            .Scan(
                emptyStats,
                (stats, msg) => stats.AddData(msg, this.scheduler.Now - start));

        // Subscriptions will expire upon completion, no need for explicit disposal.
        _ = cumulativeStatistics
            .Sample(this.samplingInterval)
            .ObserveOn(this.scheduler)
            .Subscribe(this.statisticsObserver);
    }
}