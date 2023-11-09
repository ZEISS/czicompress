// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using AsyncCommand = ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>;

/// <summary>
/// A view model for the execution control.
/// </summary>
public interface IExecutionControlViewModel
{
    public AsyncCommand StartCommand { get; }

    public AsyncCommand StopCommand { get; }
}