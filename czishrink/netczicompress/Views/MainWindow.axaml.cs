// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Views;

using System.Reactive;
using System.Reactive.Linq;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;

using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;

using AsyncCommand = ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>;

/// <summary>
/// The main window of the application.
/// </summary>
public partial class MainWindow : Window
{
    public static readonly StyledProperty<AsyncCommand?> StopRunningTasksCommandProperty =
        AvaloniaProperty.Register<MainWindow, AsyncCommand?>(nameof(StopRunningTasksCommand));

    /// <summary>
    /// A routed event that requests a notification to be shown.
    /// </summary>
    public static readonly RoutedEvent<NotificationEventArgs> NotificationRequestedEvent =
        RoutedEvent.Register<NotificationEventArgs>(
            nameof(NotificationRequested),
            RoutingStrategies.Bubble,
            typeof(MainWindow));

    private INotificationManager? manager;

    public MainWindow()
    {
        this.InitializeComponent();

        this.Closing += this.OnClosing;
        this.NotificationRequested += this.OnNotificationRequested;
    }

    /// <summary>
    /// Occurs when a notification is requested on this control or one of its children.
    /// </summary>
    public event EventHandler<NotificationEventArgs> NotificationRequested
    {
        add => this.AddHandler(NotificationRequestedEvent, value);
        remove => this.RemoveHandler(NotificationRequestedEvent, value);
    }

    public AsyncCommand? StopRunningTasksCommand
    {
        get => this.GetValue(StopRunningTasksCommandProperty);
        set => this.SetValue(StopRunningTasksCommandProperty, value);
    }

    private INotificationManager NotificationManager
    {
        get
        {
            if (this.manager == null)
            {
                var newInstance = new WindowNotificationManager(this)
                {
                    Position = NotificationPosition.BottomRight,
                };

                // If we do not call ApplyTemplate
                // the first notification will not be shown.
                newInstance.ApplyTemplate();

                this.manager = newInstance;
            }

            return this.manager;
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (!this.HasRunningTasks())
        {
            // OK to close immediately
            return;
        }

        // We cannot close right now => cancel this call to Close()
        args.Cancel = true;

        if (await this.UserConfirmsStopRunningTasksAndClose())
        {
            this.IsEnabled = false;
            await this.StopRunningTasksAsync();
            this.Close();
        }
    }

    private async Task StopRunningTasksAsync()
    {
        if (this.HasRunningTasks())
        {
            await this.StopRunningTasksCommand!.Execute(Unit.Default);
        }
    }

    private bool HasRunningTasks()
    {
        ICommand? synchronousCommand = this.StopRunningTasksCommand;
        return synchronousCommand?.CanExecute(null) ?? false;
    }

    private async Task<bool> UserConfirmsStopRunningTasksAndClose()
    {
        var contentMessage = $"Do you want to stop the running task and close {this.Title}?";

        var box = MessageBoxManager.GetMessageBoxCustom(
            new MessageBoxCustomParams
            {
                Icon = MsBox.Avalonia.Enums.Icon.Stop,
                ContentTitle = "Folder Operation in Progress",
                ContentHeader = "Stop the current operation?",
                ContentMessage = contentMessage,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ButtonDefinitions = new[]
                {
                    new ButtonDefinition { Name = "Yes", IsDefault = false },
                    new ButtonDefinition { Name = "No", IsDefault = true, IsCancel = true },
                },
                WindowIcon = this.Icon!,
            });

        var result = await box.ShowWindowDialogAsync(this);
        return result == "Yes";
    }

    private void OnNotificationRequested(object? sender, NotificationEventArgs e)
    {
        this.NotificationManager.Show(e.Notification);
    }
}