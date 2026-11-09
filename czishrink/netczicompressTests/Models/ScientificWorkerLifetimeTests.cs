// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>Exercises actual worker lifetime and locked-scratch failure paths.</summary>
public class ScientificWorkerLifetimeTests
{
    [PortableWorkerTheory]
    [InlineData("callback")]
    [InlineData("callback_json")]
    [InlineData("child")]
    [InlineData("malformed")]
    public void AbnormalExitStopsWorkersBeforeReturning(string scenario)
    {
        using var fixture = new WorkerFixture(scenario);
        using var worker = new ScientificFileProcessor(CompressionMode.CompressAll, CompressionLevel.Default, fixture.Tools);
        Exception original = scenario == "callback_json"
            ? new System.Text.Json.JsonException("Original progress observer failure")
            : new InvalidOperationException("Original progress observer failure");
        Action process = () => worker.ProcessFile(fixture.Source, fixture.Output, _ => throw original, CancellationToken.None);

        Exception? failure = Record.Exception(process);

        if (scenario != "malformed")
        {
            failure.Should().BeSameAs(original);
        }
        else
        {
            failure.Should().BeOfType<InvalidOperationException>();
        }

        fixture.AssertWorkersExited();
        File.Exists(fixture.Output).Should().BeFalse();
        Directory.GetDirectories(fixture.Root, ".czishrink-tools-*").Should().BeEmpty();
        File.ReadAllText(fixture.Source).Should().Be("unchanged synthetic input");
    }

    [PortableWorkerFact]
    public void CancellationWaitsForWorkerExitAndLeavesNoOutput()
    {
        using var fixture = new WorkerFixture("cancel");
        using var cancellation = new CancellationTokenSource();
        using var worker = new ScientificFileProcessor(CompressionMode.CompressAll, CompressionLevel.Default, fixture.Tools);
        Action process = () => worker.ProcessFile(fixture.Source, fixture.Output, _ => cancellation.Cancel(), cancellation.Token);

        process.Should().Throw<OperationCanceledException>();
        fixture.AssertWorkersExited();
        File.Exists(fixture.Output).Should().BeFalse();
        Directory.GetDirectories(fixture.Root, ".czishrink-tools-*").Should().BeEmpty();
    }

    [PortableWorkerTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void LockedScratchDoesNotReplaceCodecFailureOrInvalidateVerifiedCopy(bool success)
    {
        using var fixture = new WorkerFixture(success ? "locked_success" : "locked_error");
        using var worker = new ScientificFileProcessor(CompressionMode.CompressAll, CompressionLevel.Default, fixture.Tools);
        FileStream? locked = null;
        try
        {
            Action process = () => worker.ProcessFile(
                fixture.Source,
                fixture.Output,
                _ => locked = new FileStream(File.ReadAllText(Path.Join(fixture.Root, "lock-path.txt")), FileMode.Open, FileAccess.Read, FileShare.Read),
                CancellationToken.None);
            if (success)
            {
                process.Should().NotThrow();
                File.ReadAllBytes(fixture.Output).Should().Equal(File.ReadAllBytes(fixture.Source));
                worker.LastDetails!.ContainerByteIdentical.Should().BeTrue();
            }
            else
            {
                process.Should().Throw<IOException>().WithMessage("Original synthetic codec failure");
            }

            fixture.AssertWorkersExited();
            Directory.GetDirectories(fixture.Root, ".czishrink-tools-*").Should().ContainSingle();
        }
        finally
        {
            locked?.Dispose();
        }
    }

    private sealed class WorkerFixture : IDisposable
    {
        private readonly DateTime created = DateTime.Now;

        public WorkerFixture(string scenario)
        {
            string parent = Environment.GetEnvironmentVariable("CZISHRINK_TEST_DIRECTORY") ?? Path.GetTempPath();
            this.Root = Path.GetFullPath(Path.Join(parent, "worker-lifetime-" + Guid.NewGuid().ToString("N")));
            this.Tools = Path.Join(this.Root, "tools");
            Directory.CreateDirectory(Path.Join(this.Tools, "python"));
            string sourcePython = Path.GetDirectoryName(Environment.GetEnvironmentVariable("CZISHRINK_TEST_PYTHON"))!;
            foreach (string file in Directory.GetFiles(sourcePython))
            {
                File.Copy(file, Path.Join(this.Tools, "python", Path.GetFileName(file)));
            }

            File.Copy(Path.Join(AppContext.BaseDirectory, "controlled_worker.py"), Path.Join(this.Tools, "scientific_worker.py"));
            File.WriteAllText(Path.Join(this.Tools, "scenario.txt"), scenario);
            File.WriteAllText(this.Source, "unchanged synthetic input");
        }

        public string Root { get; }

        public string Tools { get; }

        public string Source => Path.Join(this.Root, "synthetic.h5");

        public string Output => Path.Join(this.Root, "new-output.h5");

        public void AssertWorkersExited()
        {
            var pids = Directory.GetFiles(this.Root, "pid-*.txt");
            pids.Should().NotBeEmpty();
            foreach (string pidFile in pids)
            {
                try
                {
                    using var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile)));
                    child.WaitForExit(5000).Should().BeTrue();
                }
                catch (ArgumentException)
                {
                    // An already-reaped PID is also an exited worker.
                }
            }
        }

        public void Dispose()
        {
            foreach (string pidFile in Directory.GetFiles(this.Root, "pid-*.txt"))
            {
                try
                {
                    using var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile)));
                    string expected = Path.Join(this.Tools, "python", "python.exe");
                    if (!child.HasExited && child.StartTime >= this.created
                        && string.Equals(child.MainModule?.FileName, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        child.Kill(entireProcessTree: true);
                        child.WaitForExit(5000);
                    }
                }
                catch (Exception error) when (error is ArgumentException or InvalidOperationException or Win32Exception)
                {
                    // The owned worker may have exited between PID lookup and cleanup.
                }
            }

            string parent = Path.GetFullPath(Environment.GetEnvironmentVariable("CZISHRINK_TEST_DIRECTORY") ?? Path.GetTempPath());
            if (!this.Root.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(this.Root).StartsWith("worker-lifetime-", StringComparison.Ordinal))
            {
                throw new IOException("Refusing to clean a path outside the owned local test fixture.");
            }

            // Windows may briefly retain a mapped DLL after a killed process exits.
            var cleanupTime = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    Directory.Delete(this.Root, recursive: true);
                    break;
                }
                catch (Exception error) when ((error is IOException or UnauthorizedAccessException) && cleanupTime.Elapsed < TimeSpan.FromSeconds(5))
                {
                    Thread.Sleep(50);
                }
            }
        }
    }
}
