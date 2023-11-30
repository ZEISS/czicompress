// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.ComponentModel;

using netczicompress.ViewModels;

/// <summary>
/// Tests for <see cref="AggregateStatisticsViewModel"/>.
/// </summary>
public class AggregateStatisticsViewModelTests
{
    [Fact]
    public void Properties_AfterCreation_HaveExpectedValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        // ACT
        var sut = fixture.Create<AggregateStatisticsViewModel>();

        // ASSERT
        sut.Should().BeEquivalentTo(AggregateStatistics.Empty);
    }

    [Fact]
    public void OnNext_WhenCalled_ChangesPropertyValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<AggregateStatisticsViewModel>();
        var newValue = fixture.Create<AggregateStatistics>();
        var propertyListener = new Mock<PropertyChangedEventHandler>();
        sut.PropertyChanged += propertyListener.Object;

        // ACT
        sut.OnNext(newValue);

        // ASSERT
        propertyListener.Verify(
            x => x.Invoke(
                    sut,
                    It.Is<PropertyChangedEventArgs>(e => e.PropertyName == string.Empty)),
            Times.Once);
        propertyListener.VerifyNoOtherCalls();

        sut.Should().BeEquivalentTo(newValue);
    }

    [Theory]
    [InlineData(12, 13, 14, 15, 0, "301h 14m 15s")]
    [InlineData(0, 13, 14, 15, 0, "13h 14m 15s")]
    [InlineData(0, 3, 14, 15, 0, "3h 14m 15s")]
    [InlineData(0, 0, 04, 05, 10, "4m 5s")]
    [InlineData(10, 25, 62, 64, 10000, "266h 3m 14s")]
    [InlineData(00, 00, 00, 00, int.MaxValue, "596h 31m 23s")]
    [InlineData(00, 256204778, 48, 5, 477, "256204778h 48m 5s")] /* roughly equivalent to TimeSpan.MaxValue */
    [InlineData(0, -13, -14, -15, 0, "-13h -14m -15s")] /* Should be impossible but it doesn't hurt to handle */
    public void OnNext_WithNewDuration_HasNiceFormattedPropertySet(int days, int hours, int minutes, int seconds, int milliseconds, string formattedString)
    {
        // ARRANGE
        var fixture = CreateFixture();
        var sut = fixture.Create<AggregateStatisticsViewModel>();
        var newValue = fixture.Create<AggregateStatistics>();
        var modifiedValue = newValue with { Duration = new TimeSpan(days, hours, minutes, seconds, milliseconds) };

        // ACT
        sut.OnNext(modifiedValue);

        // ASSERT
        sut.Duration.Should().BeEquivalentTo(formattedString);
    }

    [Fact]
    public void OnCompleted_WhenCalled_DoesNotDoAnything()
    {
        WhenCalled_DoesNotDoAnything((sut, _) => sut.OnCompleted());
    }

    [Fact]
    public void OnError_WhenCalled_DoesNotDoAnything()
    {
        WhenCalled_DoesNotDoAnything((sut, fixture) => sut.OnError(fixture.Create<Exception>()));
    }

    [Fact]
    public void AggregateIndicationViewModel_WhenGot_IsInjectedInstance()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        var injected = fixture.Freeze<IAggregateIndicationViewModel>();

        // ACT
        var sut = fixture.Create<AggregateStatisticsViewModel>();

        // ASSERT
        sut.AggregateIndicationViewModel.Should().BeSameAs(injected);
    }

    private static void WhenCalled_DoesNotDoAnything(Action<IObserver<AggregateStatistics>, IFixture> act)
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        var oldValue = fixture.Create<AggregateStatistics>();
        var sut = fixture.Create<AggregateStatisticsViewModel>();
        sut.OnNext(oldValue);

        // ACT
        using var monitor = sut.Monitor();
        act(sut, fixture);

        // ASSERT
        monitor.OccurredEvents.Should().BeEmpty();
        sut.Should().BeEquivalentTo(oldValue);
    }

    private static IFixture CreateFixture()
    {
        // ARRANGE
        return new Fixture().Customize(new AutoMoqCustomization());
    }
}
