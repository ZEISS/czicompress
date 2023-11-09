// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

/// <summary>
/// Extensions for <see cref="IFolderCompressor"/>.
/// </summary>
public static class FolderCompressorExtensions
{
    public static IFolderCompressor Decorate<TMessage>(this IFolderCompressor self, Action<FolderCompressorParameters, IObservable<TMessage>> observeRun)
        where TMessage : CompressorMessage
    {
        return new FolderCompressorDecorator
        {
            Core = self,
            ObserveRun = (parameters, output) => observeRun(parameters, output.OfType<TMessage>()),
        };
    }

    public static IFolderCompressor DecorateWithRunObserver<TMessage>(this IFolderCompressor self, IFolderCompressorRunObserver<TMessage> runObserver)
        where TMessage : CompressorMessage
    {
        return self.Decorate<TMessage>(runObserver.ObserveRun);
    }

    public static IFolderCompressor DecorateWithObserver<TMessage>(this IFolderCompressor self, IObserver<TMessage> observer, IScheduler scheduler)
        where TMessage : CompressorMessage
    {
        return self.Decorate<TMessage>((parameters, output) => output.ObserveOn(scheduler).Subscribe(observer));
    }
}
