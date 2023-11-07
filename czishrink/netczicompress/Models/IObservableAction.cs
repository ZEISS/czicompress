// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;

/// <summary>
/// An action that produces a sequence of values.
/// </summary>
/// <typeparam name="T">The type of the produced items.</typeparam>
public interface IObservableAction<out T>
{
    /// <summary>
    /// Gets the output sequence.
    /// </summary>
    /// <remarks>
    /// This observable will terminate when the action completes.
    /// </remarks>
    IObservable<T> Output { get; }

    /// <summary>
    /// Starts the action. The <see cref="Output"/> must complete when the action has finished.
    /// </summary>
    /// <remarks>
    /// This method can only be called once.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when this method is called more than once.</exception>
    void Start();
}