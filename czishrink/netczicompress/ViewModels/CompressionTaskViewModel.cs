// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using ReactiveUI;

/// <summary>
/// A view model for an in-progress single-file operation.
/// </summary>
public class CompressionTaskViewModel : ViewModelBase, ICompressionTaskViewModel
{
    private readonly ObservableAsPropertyHelper<int> progressPercentPropertyHelper;
    private readonly IObservable<bool> onCompleted;
    private readonly ObservableAsPropertyHelper<bool> isCompletedPropertyHelper;
    private readonly ObservableAsPropertyHelper<bool> isIndeterminateHelper;

    public CompressionTaskViewModel(string fileName, IObservable<int> percentComplete)
    {
        this.FileName = fileName;

        this.progressPercentPropertyHelper = percentComplete
            .ToProperty(source: this, property: nameof(this.ProgressPercent));

        this.isIndeterminateHelper = percentComplete
            .Select(p => p <= 1)
            .DistinctUntilChanged()
            .ToProperty(source: this, property: nameof(this.IsIndeterminateProgress));

        this.onCompleted = percentComplete
            .Select(p => p == 100)
            .DistinctUntilChanged();

        this.isCompletedPropertyHelper = this.onCompleted
            .ToProperty(source: this, property: nameof(this.IsCompleted));
    }

    public string FileName { get; }

    public int ProgressPercent => this.progressPercentPropertyHelper.Value;

    public bool IsIndeterminateProgress => this.isIndeterminateHelper.Value;

    public bool IsCompleted => this.isCompletedPropertyHelper.Value;

    public void AddWhileNotCompleted(IList<ICompressionTaskViewModel> list)
    {
        if (!this.IsCompleted)
        {
            list.Add(this);
            this.onCompleted.Subscribe(onNext: value =>
            {
                if (value)
                {
                    list.Remove(this);
                }
            });
        }
    }
}