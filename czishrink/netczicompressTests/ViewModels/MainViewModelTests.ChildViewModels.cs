// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using AutoFixture;

using netczicompress.ViewModels;

/// <summary>
/// Tests for <see cref="MainViewModel"/>.
/// </summary>
/// <content>Tests for child view models.</content>
public partial class MainViewModelTests
{
    [Fact]
    public void ChildViewModels_AreThoseInjected()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var errorList = fixture.Freeze<IErrorListViewModel>();
        var currentTasks = fixture.Freeze<ICurrentTasksViewModel>();
        var aggregateStatistics = fixture.Freeze<IAggregateStatisticsViewModel>();
        var about = fixture.Freeze<IAboutViewModel>();

        // ACT
        var sut = fixture.Create<MainViewModel>();

        // ASSERT
        sut.About.Should().BeSameAs(about);
        sut.AggregateStatisticsViewModel.Should().BeSameAs(aggregateStatistics);
        sut.CurrentTasks.Should().BeSameAs(currentTasks);
        sut.ErrorList.Should().BeSameAs(errorList);
    }
}
