// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.Windows.Input;

using netczicompress.Models;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

/// <summary>
/// View model for version and license information.
/// </summary>
public class AboutViewModel : ViewModelBase, IAboutViewModel
{
    private readonly IFileLauncher launcher;

    public AboutViewModel(IFileLauncher launcher, IProgramNameAndVersion info)
    {
        this.CloseAboutCommand = ReactiveCommand.Create(() => this.IsVisible = false);
        this.ShowAboutCommand = ReactiveCommand.Create(() => this.IsVisible = true);
        this.ShowTextFileCommand = ReactiveCommand.CreateFromTask<string>(this.OpenTextFile);
        this.ProgramVersionAndCopyRight = $"{info}, © 2023 Carl Zeiss Microscopy GmbH and others";
        this.launcher = launcher;
    }

    [Reactive]
    public bool IsVisible { get; set; } = false;

    public required string LibraryName { get; init; }

    public string ProgramVersionAndCopyRight { get; }

    public ICommand CloseAboutCommand { get; }

    public ICommand ShowAboutCommand { get; }

    public ICommand ShowTextFileCommand { get; }

    private Task OpenTextFile(string filename)
    {
        var fullFilename = System.IO.Path.Combine(AppContext.BaseDirectory, filename);
        return this.launcher.Launch(fullFilename);
    }
}
