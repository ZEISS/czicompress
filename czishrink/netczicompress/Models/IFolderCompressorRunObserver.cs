// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;

/// <summary>
/// An object that can observe folder compressor runs.
/// </summary>
/// <typeparam name="TMessage">The type of messages to observe.</typeparam>
public interface IFolderCompressorRunObserver<in TMessage>
    where TMessage : CompressorMessage
{
    void ObserveRun(FolderCompressorParameters runParameters, IObservable<TMessage> runMessages);
}
