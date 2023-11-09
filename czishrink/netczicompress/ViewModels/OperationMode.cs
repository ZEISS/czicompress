// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using netczicompress.Models;

public record OperationMode(CompressionMode Value, string DisplayText, string ToolTipText)
{
    public override string ToString()
    {
        return this.DisplayText;
    }
}
