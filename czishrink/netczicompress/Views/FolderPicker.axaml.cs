// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Views;

using System.Diagnostics;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

/// <summary>
/// A control that allows the user to pick a folder.
/// </summary>
public partial class FolderPicker : UserControl
{
    public static readonly StyledProperty<string> FolderProperty =
    AvaloniaProperty.Register<FolderPicker, string>(nameof(Folder), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FolderPicker, string>(nameof(Title));

    public FolderPicker() => this.InitializeComponent();

    public string Folder
    {
        get => this.GetValue(FolderProperty);
        set => this.SetValue(FolderProperty, value);
    }

    public string Title
    {
        get => this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    private async void OnButtonClicked(object source, RoutedEventArgs args)
    {
        try
        {
            await this.OpenFolderPicker();
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
        }
    }

    private async Task OpenFolderPicker()
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel == null)
        {
            return;
        }

        IStorageFolder? startLocation;
        try
        {
            startLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(this.Folder);
        }
        catch
        {
            startLocation = null;
        }

        // Start async operation to open the dialog.
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = this.Title,
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
        });

        if (folders.Count >= 1)
        {
            this.Folder = folders[0].TryGetLocalPath() ?? folders[0].Path.ToString();
        }
    }
}
