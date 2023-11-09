// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Views;

using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;

/// <summary>
/// Routed event arguments with an <see cref="INotification"/> payload.
/// </summary>
/// <seealso cref="MainWindow.NotificationRequestedEvent"/>
/// <seealso cref="MainWindow.NotificationRequested"/>
public class NotificationEventArgs : RoutedEventArgs
{
    public NotificationEventArgs(INotification notification)
    {
        this.Notification = notification;
    }

    public INotification Notification { get; }
}