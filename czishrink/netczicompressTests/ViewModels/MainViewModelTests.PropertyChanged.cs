// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.ComponentModel;

using AutoFixture;

using netczicompress.ViewModels;

/// <summary>
/// Tests for <see cref="MainViewModel"/>.
/// </summary>
/// <content>Tests for <see cref="INotifyPropertyChanged"/>.</content>
public partial class MainViewModelTests
{
    [Fact]
    public void InputDirectory_WhenSet_RaisesPropertyChanged()
    {
        // ARRANGE
        var fixture = CreateFixture();
        var sut = fixture.Create<MainViewModel>();

        // ACT
        using var monitor = sut.Monitor();
        sut.InputDirectory = "foo";

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.InputDirectory);
    }

    [Fact]
    public void OutputDirectory_WhenSet_RaisesPropertyChanged()
    {
        // ARRANGE
        var fixture = CreateFixture();
        var sut = fixture.Create<MainViewModel>();

        // ACT
        using var monitor = sut.Monitor();
        sut.OutputDirectory = "foo";

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.OutputDirectory);
    }

    [Fact]
    public void Recursive_WhenSet_RaisesPropertyChanged()
    {
        // ARRANGE
        var fixture = CreateFixture();
        var sut = fixture.Create<MainViewModel>();

        // ACT
        using var monitor = sut.Monitor();
        sut.Recursive = !sut.Recursive;

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.Recursive);
    }

    [Theory]
    [AutoData]
    public void SelectedMode_WhenSet_RaisesPropertyChanged(OperationMode mode)
    {
        // ARRANGE
        var fixture = CreateFixture();
        var sut = fixture.Create<MainViewModel>();

        // ACT
        using var monitor = sut.Monitor();
        sut.SelectedMode = mode;

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.SelectedMode);
    }
}
