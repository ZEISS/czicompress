// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models.Mocks;

using System.Reactive.Concurrency;
using System.Reactive.Disposables;

/// <summary>
/// A scheduler whose queue must be run explicitly.
/// </summary>
public class SchedulerMock : IScheduler
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    private Queue<Action> Queue { get; } = new();

    public void RunQueuedActions()
    {
        while (this.Queue.TryDequeue(out var action))
        {
            action();
        }
    }

    public IDisposable Schedule<TState>(TState state, Func<IScheduler, TState, IDisposable> action)
    {
        this.Queue.Enqueue(() => _ = action(this, state));
        return Disposable.Empty;
    }

    public IDisposable Schedule<TState>(TState state, TimeSpan dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        throw new NotImplementedException();
    }

    public IDisposable Schedule<TState>(TState state, DateTimeOffset dueTime, Func<IScheduler, TState, IDisposable> action)
    {
        throw new NotImplementedException();
    }
}
