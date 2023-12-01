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
        sut.Should().BeEquivalentTo(AggregateStatistics.Empty, options => options.Excluding(o => o.Duration));
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

        sut.Should().BeEquivalentTo(newValue, options => options.Excluding(o => o.Duration));
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

        // Duration formatting is tested in <see cref="OnNext_WithNewDuration_HasNiceFormattedPropertySet"/>
        sut.Should().BeEquivalentTo(oldValue, options => options.Excluding(o => o.Duration));
    }

    private static IFixture CreateFixture()
    {
        // ARRANGE
        return new Fixture().Customize(new AutoMoqCustomization());
    }
}
