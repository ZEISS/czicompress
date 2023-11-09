// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// An adapter from an <see cref="IAsyncEnumerable{T}"/> to an <see cref="IObservableAction{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the produced items.</typeparam>
public class AsyncEnumerableToObservableActionAdapter<T> : IObservableAction<T>
{
    private readonly IAsyncEnumerable<T> enumerable;
    private readonly CancellationToken token;
    private readonly Subject<T> subject = new();
    private bool started;

    public AsyncEnumerableToObservableActionAdapter(IAsyncEnumerable<T> enumerable, CancellationToken token)
    {
        this.enumerable = enumerable;
        this.token = token;
        this.Output = this.subject.AsObservable();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This observable may deliver items on another thread than the thread that calls <see cref="Start"/>.
    /// <para/>
    /// Use an appropriate <see cref="IScheduler"/> to observe it.
    /// </remarks>
    public IObservable<T> Output { get; }

    /// <inheritdoc/>
    public void Start()
    {
        if (this.started)
        {
            throw new InvalidOperationException($"{nameof(this.Start)} can only be called once.");
        }

        this.started = true;

        Core();

        async void Core()
        {
            try
            {
                await foreach (var item in this.enumerable.WithCancellation(this.token).ConfigureAwait(false))
                {
                    this.subject.OnNext(item);
                }

                this.subject.OnCompleted();
            }
            catch (Exception ex)
            {
                this.subject.OnError(ex);
            }
        }
    }
}
