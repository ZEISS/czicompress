// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;

using DynamicData.Binding;

using netczicompress.Models;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

/// <summary>
/// A view model for all errors occurring during a folder compression operation.
/// </summary>
/// <remarks>
/// This class buffers incoming messages in order to coalesce many
/// errors in rapid succession into just a few (costly) GUI updates.
/// </remarks>
public class ErrorListViewModel
    : ViewModelBase,
        IErrorListViewModel,
        IFolderCompressorRunObserver<CompressorMessage.FileFinished>
{
    private readonly TimeSpan bufferInterval;
    private readonly int bufferCapacity;
    private readonly IScheduler scheduler;
    private readonly IFileLauncher launcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorListViewModel"/> class.
    /// </summary>
    /// <param name="maxNumberOfErrorsToShow">The maximum number of errors to show.</param>
    /// <param name="bufferInterval">The buffering interval, determines the GUI update frequency.</param>
    /// <param name="bufferCapacity">
    /// The buffer capacity, should be larger than <see cref="BulkAddThreshold"/> for buffering to be effective.
    /// </param>
    /// <param name="scheduler">The scheduler used to update the public properties of this view model.</param>
    /// <param name="launcher">The file launcher to use.</param>
    public ErrorListViewModel(int maxNumberOfErrorsToShow, TimeSpan bufferInterval, int bufferCapacity, IScheduler scheduler, IFileLauncher launcher)
    {
        this.MaxNumberOfErrorsToShow = maxNumberOfErrorsToShow;
        this.bufferInterval = bufferInterval;
        this.bufferCapacity = bufferCapacity;
        this.scheduler = scheduler;
        this.launcher = launcher;
        this.Errors = new ReadOnlyObservableCollection<ErrorItem>(this.ErrorsCollection);

        var hasSelectedErrorItem = this.WhenValueChanged(x => x.SelectedErrorItem).Select(x => x != null);
        this.ShowSelectedFileCommand = ReactiveCommand.CreateFromTask(
            this.ShowSelectedFile,
            hasSelectedErrorItem);
        this.OpenSelectedFileCommand = ReactiveCommand.CreateFromTask(
            this.OpenSelectedFile,
            hasSelectedErrorItem);
    }

    public int MaxNumberOfErrorsToShow { get; }

    /// <inheritdoc/>
    public ICommand ShowSelectedFileCommand { get; }

    /// <inheritdoc/>
    public ICommand OpenSelectedFileCommand { get; }

    /// <summary>
    /// Gets the minimum number of buffered items that triggers a bulk add.
    /// </summary>
    /// <remarks>
    /// When more than <see cref="BulkAddThreshold"/> items are added,
    /// observers of the <see cref="Errors"/> collection will be notified
    /// with a single Reset event instead of multiple single-item adds.
    /// <para/>
    /// The default value of this property is 20.
    /// </remarks>
    public int BulkAddThreshold { get; init; } = 20;

    public ReadOnlyObservableCollection<ErrorItem> Errors { get; }

    [Reactive]
    public ErrorItem? SelectedErrorItem { get; set; }

    private ObservableCollectionExtended<ErrorItem> ErrorsCollection { get; } = new();

    public void ObserveRun(
        FolderCompressorParameters runParameters,
        IObservable<CompressorMessage.FileFinished> runMessages)
    {
        _ = runParameters;
        this.Reset(runMessages);
    }

    private static ErrorItem CreateTruncationItem()
    {
        return new ErrorItem
        {
            File = "…",
            ErrorMessage = "See log file for further errors.",
        };
    }

    private void Reset(IObservable<CompressorMessage.FileFinished> runMessages)
    {
        this.SelectedErrorItem = null;
        this.ErrorsCollection.Clear();

        var runOutput =
            runMessages
                .CompleteOnError();
        var errorItems =
            from message in runOutput
            let errorMessage = message.ErrorMessage
            where errorMessage != null
            let file = message.InputFile.FullName
            select new ErrorItem { File = file, ErrorMessage = errorMessage };

        // We are buffering here to make sure that the UI remains
        // responsive when we are rapidly creating error messages.
        var bufferedErrorItems =
            errorItems
                .Buffer(this.bufferInterval, this.bufferCapacity)
                .Where(items => items.Count != 0);

        var subscribeCts = new CancellationTokenSource();
        bufferedErrorItems
                .ObserveOn(this.scheduler)
                .Subscribe(
                    items => this.AddItems(items, subscribeCts.Cancel),
                    subscribeCts.Token);
    }

    private void AddItems(IList<ErrorItem> items, Action stopObserving)
    {
        var remainingItemsToShow = this.MaxNumberOfErrorsToShow - this.ErrorsCollection.Count;
        if (remainingItemsToShow < items.Count)
        {
            // We have reached the maximum number of errors to show.
            // => Unsubscribe from the run output.
            stopObserving();
        }

        var numberOfItemsToAdd = Math.Min(
            remainingItemsToShow,
            items.Count);

        if (numberOfItemsToAdd <= 0)
        {
            return;
        }

        using (numberOfItemsToAdd > this.BulkAddThreshold ? this.ErrorsCollection.SuspendNotifications() : null)
        {
            this.ErrorsCollection.AddRange(items.Take(numberOfItemsToAdd));
        }

        this.SelectedErrorItem = this.ErrorsCollection[^1];

        if (this.ErrorsCollection.Count == this.MaxNumberOfErrorsToShow)
        {
            this.ErrorsCollection.Add(
                CreateTruncationItem());
        }
    }

    private async Task ShowSelectedFile()
    {
        if (this.SelectedErrorItem != null)
        {
            await this.launcher.Reveal(this.SelectedErrorItem.File);
        }
    }

    private async Task OpenSelectedFile()
    {
        if (this.SelectedErrorItem != null)
        {
            await this.launcher.Launch(this.SelectedErrorItem.File);
        }
    }
}
