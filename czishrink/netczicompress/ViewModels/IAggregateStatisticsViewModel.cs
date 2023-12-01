// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.ComponentModel;
using System.Windows.Input;

using netczicompress.Models;

/// <summary>
/// A view model that wraps a sequence of <see cref="AggregateStatistics"/>.
/// </summary>
public interface IAggregateStatisticsViewModel : INotifyPropertyChanged
{
    public event Action? BadgeCopiedToClipboardRaised;

    long DeltaBytes { get; }

    TimeSpan Duration { get; }

    string FormattedDuration { get; }

    int FilesWithErrors { get; }

    int FilesWithNoErrors { get; }

    long InputBytes { get; }

    long OutputBytes { get; }

    float OutputToInputRatio { get; }

    ICommand OpenLogFileCommand { get; }

    ICommand CopyBadgeToClipboardCommand { get; }

    IAggregateIndicationViewModel AggregateIndicationViewModel { get; }

    bool IsCopyBadgeToClipboardCommandVisible { get; }
}
