// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// Extensions for observable sequences.
/// </summary>
public static partial class ObservableMixin
{
    /// <summary>
    /// Creates an observable that will call <see cref="IObserver{T}.OnCompleted"/>
    /// when the current sequence is terminated with an error.
    /// </summary>
    /// <remarks>
    /// This should be equivalent to <c>obs.Catch(Observable.Empty&lt;T&gt;)</c>,
    /// but I had problems (NullReferenceExceptions) with that code in my tests and so chose to
    /// write this extension.
    /// </remarks>
    /// <typeparam name="T">The type of the items in the sequence.</typeparam>
    /// <param name="obs">The source sequence.</param>
    /// <returns>The wrapped source sequence.</returns>
    public static IObservable<T> CompleteOnError<T>(this IObservable<T> obs)
    {
        return new CompleteOnErrorDecorator<T>(obs);
    }

    public class CompleteOnErrorDecorator<T>(IObservable<T> core) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            return core.Subscribe(
                onNext: observer.OnNext,
                onCompleted: observer.OnCompleted,
                onError: ex => observer.OnCompleted());
        }
    }
}
