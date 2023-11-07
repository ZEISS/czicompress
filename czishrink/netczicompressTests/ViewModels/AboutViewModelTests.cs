// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using netczicompress.ViewModels;

/// <summary>
/// Tests for <see cref="AboutViewModel"/>.
/// </summary>
public class AboutViewModelTests
{
    [Fact]
    public void AfterConstruction_HasExpectedProperties()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        var launcher = fixture.Create<IFileLauncher>();
        var versionInfo = fixture.Create<Mock<IProgramNameAndVersion>>();
        var libname = fixture.Create<string>();
        versionInfo.Setup(x => x.ToString()).Returns("FooBar 99.3.7");

        // ACT
        var sut = new AboutViewModel(launcher, versionInfo.Object) { LibraryName = libname };

        // ASSERT
        sut.IsVisible.Should().BeFalse();
        sut.LibraryName.Should().BeSameAs(libname);
        sut.ProgramVersionAndCopyRight.Should().Be("FooBar 99.3.7, © 2023 Carl Zeiss Microscopy GmbH and others");
    }

    [Theory]
    [AutoData]
    public void IsVisible_WhenSet_RaisesEvent(bool initialValue)
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<AboutViewModel>();
        sut.IsVisible = initialValue;
        using var monitor = sut.Monitor();

        // ACT
        sut.IsVisible = !initialValue;

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.IsVisible);
        sut.IsVisible.Should().Be(!initialValue);
    }

    [Fact]
    public void ShowAboutCommand_WhenNotVisible_SetsVisibleToTrue()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<AboutViewModel>();
        sut.IsVisible = false;
        using var monitor = sut.Monitor();

        // ACT
        sut.ShowAboutCommand.CanExecute(null).Should().BeTrue();
        sut.ShowAboutCommand.Execute(null);

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.IsVisible);
        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void CloseAboutCommand_WhenVisible_SetsVisibleToFalse()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<AboutViewModel>();
        sut.IsVisible = true;
        using var monitor = sut.Monitor();

        // ACT
        sut.CloseAboutCommand.CanExecute(null).Should().BeTrue();
        sut.CloseAboutCommand.Execute(null);

        // ASSERT
        monitor.Should().RaisePropertyChangeFor(x => x.IsVisible);
        sut.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void ShowTextFileCommand_WhenCalled_LaunchesFileFromBaseDirectory()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();
        var launcherMock = fixture.Freeze<Mock<IFileLauncher>>();

        var sut = fixture.Create<AboutViewModel>();
        const string file = "foobar.ext";

        // ACT
        sut.ShowTextFileCommand.CanExecute(file).Should().BeTrue();
        sut.ShowTextFileCommand.Execute(file);

        // ASSERT
        var expectedFileName = System.IO.Path.Combine(AppContext.BaseDirectory, file);
        launcherMock.Verify(x => x.Launch(expectedFileName), Times.Once);
    }

    private static IFixture CreateFixture()
    {
        // ARRANGE
        return new Fixture().Customize(new AutoMoqCustomization());
    }
}
