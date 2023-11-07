// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Views;

using Avalonia.Controls;
using Avalonia.Controls.Notifications;

using netczicompress.ViewModels;

/// <summary>
/// A control that visualizes <see cref="netczicompress.Models.AggregateStatistics"/>.
/// </summary>
public partial class AggregateStatisticsView : UserControl
{
    public AggregateStatisticsView() => this.InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Subscribe to the new ViewModel’s event
        if (this.DataContext is IAggregateStatisticsViewModel newViewModel)
        {
            newViewModel.BadgeCopiedToClipboardRaised += this.OnNotificationBadgeCopiedToClipboardRaised;
        }
    }

    private void OnNotificationBadgeCopiedToClipboardRaised()
    {
        var notification =
                       new Notification(
                           title: "Badge copied to clipboard",
                           message: null,
                           type: NotificationType.Success,
                           expiration: TimeSpan.FromSeconds(1.5));
        this.RaiseEvent(
            new NotificationEventArgs(notification)
            {
                RoutedEvent = MainWindow.NotificationRequestedEvent,
                Source = this,
            });
    }
}
