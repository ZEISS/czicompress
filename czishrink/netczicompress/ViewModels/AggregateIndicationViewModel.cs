// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;

using netczicompress.Models;

using ReactiveUI.Fody.Helpers;

public class AggregateIndicationViewModel : ViewModelBase, IObserver<AggregateStatistics>, IAggregateIndicationViewModel
{
    private AggregateStatistics current;

    [Reactive]
    public AggregateIndicationStatus IndicationStatus { get; private set; } = AggregateIndicationStatus.NotStarted;

    public void OnCompleted()
    {
        this.IndicationStatus = this.current.FilesWithErrors > 0
            ? AggregateIndicationStatus.Error
            : AggregateIndicationStatus.Success;
    }

    public void OnError(Exception error)
    {
        this.IndicationStatus = error is OperationCanceledException
            ? AggregateIndicationStatus.Cancelled
            : AggregateIndicationStatus.Error;
    }

    public void OnNext(AggregateStatistics value)
    {
        this.current = value;
        this.IndicationStatus = AggregateIndicationStatus.Started;
    }
}