// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

/// <summary>
/// A single immutable error item.
/// </summary>
public sealed class ErrorItem : ImmutableViewModeBase
{
    public required string File { get;  init; }

    public required string ErrorMessage { get; init; }

    public override string ToString()
    {
        return $"{this.File} : {this.ErrorMessage}";
    }
}
