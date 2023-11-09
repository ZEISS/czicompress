// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Views;

using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

using netczicompress.ViewModels;

/// <summary>
/// A control that visualizes a list of errors.
/// </summary>
public partial class ErrorListView : UserControl
{
    public ErrorListView() => this.InitializeComponent();

    private void ErrorListDataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid dataGrid && e.AddedItems.Count > 0)
        {
            dataGrid.ScrollIntoView(e.AddedItems[0], null);
        }

        // SelectedItems is not bindable. So for simplicity's sake, we set IsEnabled in this event handler.
        bool isExactlyOneFileSelected = this.dataGrid.SelectedItems.Count == 1;
        this.showSelectedFileMenuItem.IsEnabled = isExactlyOneFileSelected;
        this.openSelectedFileMenuItem.IsEnabled = isExactlyOneFileSelected;
    }

    private async void CopySelectedFilePaths(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await this.CopySelectedFilePaths(clipboard);
            this.NotifySelectedFilePathsCopied();
        }
    }

    private async Task CopySelectedFilePaths(IClipboard clipboard)
    {
        var paths = this.dataGrid.SelectedItems.OfType<ErrorItem>().Select(x => x.File);
        var text = string.Join(Environment.NewLine, paths);
        await clipboard.SetTextAsync(text);
    }

    private void NotifySelectedFilePathsCopied()
    {
        var notification =
                        new Notification(
                            title: "Path copied to clipboard",
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
