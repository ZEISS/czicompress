// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Views;

using Avalonia.Controls;

/// <summary>
/// A control that visualizes <see cref="netczicompress.Models.AggregateStatistics"/> with an icon and a label.
/// </summary>
public partial class AggregateIndicationView : UserControl
{
    public AggregateIndicationView() => this.InitializeComponent();
}
