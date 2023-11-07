// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;

/// <summary>
/// Static class to create composite observers.
/// </summary>
public static class CompositeObserver
{
    public static IObserver<T> Create<T>(params IObserver<T>[] observers)
    {
        return new CompositeObserverImpl<T>(observers);
    }

    /// <summary>
    /// A composite <see cref="IObserver{T}"/>.
    /// </summary>
    /// <typeparam name="T">The observed type.</typeparam>
    public sealed class CompositeObserverImpl<T> : IObserver<T>
    {
        private readonly IObserver<T>[] observers;

        public CompositeObserverImpl(params IObserver<T>[] observers)
        {
            this.observers = observers;
        }

        public void OnCompleted()
        {
            this.ForEachObserver(o => o.OnCompleted());
        }

        public void OnError(Exception error)
        {
            this.ForEachObserver(o => o.OnError(error));
        }

        public void OnNext(T value)
        {
            this.ForEachObserver(o => o.OnNext(value));
        }

        private void ForEachObserver(Action<IObserver<T>> action)
        {
            foreach (var item in this.observers)
            {
                action(item);
            }
        }
    }
}