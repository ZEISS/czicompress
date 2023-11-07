// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

/// <summary>
/// Tests for <see cref="FileLauncher"/>.
/// </summary>
public class FileLauncherTests
{
    [Fact]
    public async Task Launch_WhenCalled_RunsExpectedProcess()
    {
        // ARRANGE
        var sut = new TestableFileLauncher();
        var path = "/foo/bar/baz";

        // ACT
        await sut.Launch(path);

        // ASSERT
        var runAsyncInvocationArgs = sut.GetRunAsyncInvocationsArgs();
        runAsyncInvocationArgs.Should().BeEquivalentTo(
            new[]
            {
                new ProcessStartInfo
                {
                    FileName = "/foo/bar/baz",
                    UseShellExecute = true,
                },
            });
    }

    [Theory]
    [InlineData(null, "explorer")]
    [InlineData(@"C:\Fenster", @"C:\Fenster\explorer")]
    public async Task Reveal_WhenWindows_RunsExpectedProcess(
        string? systemRootEnvironmentVariableValue,
        string expectedFileName)
    {
        // ARRANGE
        string? overridePlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? null
            : "WINDOWS";

        var getEnv = Mock.Of<Func<string, string?>>(
            x => x("SYSTEMROOT") == systemRootEnvironmentVariableValue);

        var sut = new TestableFileLauncher(
            overridePlatform: overridePlatform,
            fs: Mock.Of<IFileSystem>(x => x.Path == MockPathCombineWindows()),
            getEnvironmentVariable: getEnv);

        var path = @"C:\foo bar\baz";

        // ACT
        await sut.Reveal(path);

        // ASSERT
        var runAsyncInvocationArgs = sut.GetRunAsyncInvocationsArgs();
        runAsyncInvocationArgs.Should().BeEquivalentTo(
            new[]
            {
                new ProcessStartInfo
                {
                    FileName = expectedFileName,
                    Arguments = @"/select,""C:\foo bar\baz""",
                },
            });
    }

    [Fact]
    public async Task Reveal_WhenOsx_RunsExpectedProcess()
    {
        // ARRANGE
        string? overridePlatform = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? null
            : "OSX";

        var sut = new TestableFileLauncher(
            overridePlatform: overridePlatform);

        var path = @"/foo bar/baz";

        // ACT
        await sut.Reveal(path);

        // ASSERT
        var runAsyncInvocationArgs = sut.GetRunAsyncInvocationsArgs();
        runAsyncInvocationArgs.Should().BeEquivalentTo(
            new[]
            {
                new ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = @"-R ""/foo bar/baz""",
                },
            });
    }

    [Theory]
    [InlineData("LINUX")]
    [InlineData("SOLARIS")]
    [InlineData("FREEBSD")]
    [InlineData("NETBSD")]
    public async Task Reveal_WhenLinux_RunsExpectedProcess(string platform)
    {
        // ARRANGE
        string? overridePlatform = OperatingSystem.IsOSPlatform(platform)
            ? null
            : platform;

        var path = @"/foo/bar baz/xyz.czi";
        Func<string, string>? overridePathToFileUri = OperatingSystem.IsOSPlatform(platform)
            ? null
            : Mock.Of<Func<string, string>>(f => f(path) == "file:///foo/bar%20baz/xyz.czi");

        var sut = new TestableFileLauncher(
            overridePathToFileUri: overridePathToFileUri,
            overridePlatform: overridePlatform);

        // ACT
        await sut.Reveal(path);

        // ASSERT
        var runAsyncInvocationArgs = sut.GetRunAsyncInvocationsArgs();
        runAsyncInvocationArgs.Should().BeEquivalentTo(
            new[]
            {
                new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = @"--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:""file:///foo/bar%20baz/xyz.czi"" string:""""",
                    UseShellExecute = true,
                },
            });
    }

    [Theory]
    [InlineData("LINUX")]
    [InlineData("SOLARIS")]
    [InlineData("FREEBSD")]
    [InlineData("NETBSD")]
    public async Task Reveal_WhenDbusSendReturnsNonZero_RunsExpectedProcess(string platform)
    {
        // ARRANGE
        string? overridePlatform = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && platform == "LINUX"
            ? null
            : platform;

        var path = "/foo/bar baz/xyz.czi";
        Func<string, string>? pathToFileUri = OperatingSystem.IsOSPlatform(platform)
            ? null
            : Mock.Of<Func<string, string>>(f => f(path) == "file:///foo/bar%20baz/xyz.czi");

        var sut = new TestableFileLauncher(
            fs: Mock.Of<IFileSystem>(x => x.Path.GetDirectoryName(path) == "/path/of/parent/dir"),
            overridePathToFileUri: pathToFileUri,
            overridePlatform: overridePlatform);

        var nonZeroExitCode = new Fixture().CreateMany<int>().First(x => x != 0);
        sut.SetupExitCode(p => p.FileName == "dbus-send", nonZeroExitCode);

        // ACT
        await sut.Reveal(path);

        // ASSERT
        var runAsyncInvocationArgs = sut.GetRunAsyncInvocationsArgs();
        runAsyncInvocationArgs.Should().BeEquivalentTo(
            new[]
            {
                new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = @"--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:""file:///foo/bar%20baz/xyz.czi"" string:""""",
                    UseShellExecute = true,
                },
                new ProcessStartInfo
                {
                    FileName = "/path/of/parent/dir",
                    UseShellExecute = true,
                },
            });
    }

    [Fact]
    public async Task Reveal_WhenOtherOs_LaunchesParentFolder()
    {
        // ARRANGE
        var path = "/foo/bar baz/xyz.czi";

        var sut = new TestableFileLauncher(
            fs: Mock.Of<IFileSystem>(x => x.Path.GetDirectoryName(path) == "/path/of/parent/dir"),
            overridePlatform: "UNSUPPORTED");

        // ACT
        await sut.Reveal(path);

        // ASSERT
        var runAsyncInvocationArgs = sut.GetRunAsyncInvocationsArgs();
        runAsyncInvocationArgs.Should().BeEquivalentTo(
            new[]
            {
                new ProcessStartInfo
                {
                    FileName = "/path/of/parent/dir",
                    UseShellExecute = true,
                },
            });
    }

    private static IPath MockPathCombineWindows()
    {
        var pathMock = new Mock<IPath>();
        pathMock
            .Setup(
                x => x.Combine(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(
                (string a, string b) => $"{a}\\{b}");
        var pathMockObject = pathMock.Object;
        return pathMockObject;
    }

    private class TestableFileLauncher : FileLauncher
    {
        private readonly Func<string, string>? pathToFileUri;

        public TestableFileLauncher(
            IFileSystem? fs = null,
            string? overridePlatform = null,
            Func<string, string>? overridePathToFileUri = null,
            Func<string, string?>? getEnvironmentVariable = null)
            : base(
                  fs ?? Mock.Of<IFileSystem>(),
                  getEnvironmentVariable ?? Mock.Of<Func<string, string?>>())
        {
            this.pathToFileUri = overridePathToFileUri;
            this.CurrentPlatform = overridePlatform ?? this.CurrentPlatform;
            this.RunAsyncMock = new Mock<Func<ProcessStartInfo, Task<int>>>();
            this.RunAsyncMock
                .Setup(x => x.Invoke(It.IsAny<ProcessStartInfo>()))
                .Returns(Task.FromResult(0));
        }

        public Mock<Func<ProcessStartInfo, Task<int>>> RunAsyncMock { get; }

        public ProcessStartInfo[] GetRunAsyncInvocationsArgs()
        {
            var result = from invocation in this.RunAsyncMock.Invocations
                         select invocation.Arguments.Cast<ProcessStartInfo>().Single();
            return result.ToArray();
        }

        public void SetupExitCode(Expression<Func<ProcessStartInfo, bool>> processStartInfoSelector, int exitCode)
        {
            this.RunAsyncMock
                .Setup(
                    x => x.Invoke(It.Is(processStartInfoSelector)))
                .Returns(Task.FromResult(exitCode));
        }

        protected override Task<int> RunAsync(ProcessStartInfo processStartInfo)
        {
            return this.RunAsyncMock.Object.Invoke(processStartInfo);
        }

        protected override string PathToFileUri(string path)
        {
            return this.pathToFileUri?.Invoke(path)
                ?? base.PathToFileUri(path);
        }
    }
}
