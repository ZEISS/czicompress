// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.Collections;
using System.Collections.Immutable;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using netczicompress.Models;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using AsyncCommand = ReactiveUI.ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit>;

/// <summary>
/// The main view model of the application.
/// </summary>
public class MainViewModel : ViewModelBase,
    IFolderInputOutputViewModel,
    IExecutionControlViewModel,
    ISettingsViewModel,
    IAdvancedOptionsViewModel
{
    private readonly IFolderCompressor compressor;
    private readonly IFileSystem fileSystem;
    private readonly ILogger logger;

    public MainViewModel(
        IFolderCompressor compressor,
        IFileSystem fileSystem,
        IErrorListViewModel errorList,
        ICurrentTasksViewModel currentTasksViewModel,
        IAggregateStatisticsViewModel aggregateStatisticsViewModel,
        IAboutViewModel aboutViewModel,
        ILogger<MainViewModel> logger)
    {
        this.ErrorList = errorList;
        this.CurrentTasks = currentTasksViewModel;
        this.AggregateStatisticsViewModel = aggregateStatisticsViewModel;
        this.About = aboutViewModel;
        this.compressor = compressor;
        this.fileSystem = fileSystem;
        this.logger = logger;

        // Create the Start command pipeline:
        var canStart =
            this.WhenAnyValue(
                x => x.InputDirectory,
                x => x.OutputDirectory,
                x => x.CurrentTask,
                (inputDir, outputDir, task) =>
                    !string.IsNullOrWhiteSpace(inputDir) && !string.IsNullOrWhiteSpace(outputDir) && task == null);
        this.StartCommand = ReactiveCommand.CreateFromTask(this.ExecuteCompressAsync, canStart);

        // Create the Stop command pipeline:
        var canStop = this.WhenAnyValue(x => x.CurrentTask)
            .Select(tsk => tsk != null);
        this.CanStopHelper = canStop.ToProperty(this, nameof(this.CanStop));
        this.StopCommand = ReactiveCommand.CreateFromTask(this.ExecuteStopAsync, canStop);

        var modes = ImmutableArray.Create<OperationMode>(
            new(CompressionMode.CompressUncompressed, "Compress uncompressed data", "Compress only uncompressed subblocks and copy others."),
            new(CompressionMode.CompressUncompressedAndZstd, "Compress uncompressed and Zstd-compressed data", "Compress subblocks that were originally compressed with zstd or uncompressed."),
            new(CompressionMode.CompressAll, "Compress all data", "Compress all subblocks regardless of current compression method (if possible)."),
            new(CompressionMode.Decompress, "Decompress all data", "Decompress all possible subblocks."),
            new(CompressionMode.NoOp, "Dry Run (enumerate and log files)", "Do not compress/decompress rather just enumerate and log files that would be affected."));
        this.Modes = modes;

        // Select the initial Compression-Mode
        this.SelectedMode = modes[0];
    }

    /// <summary>
    /// Gets the Aggregate-Statistics sub-view-model.
    /// </summary>
    public IAggregateStatisticsViewModel AggregateStatisticsViewModel { get; }

    public IAboutViewModel About { get; }

    public IErrorListViewModel ErrorList { get; }

    public ICurrentTasksViewModel CurrentTasks { get; }

    /// <inheritdoc/>
    public AsyncCommand StartCommand { get; }

    /// <inheritdoc/>
    public AsyncCommand StopCommand { get; }

    /// <inheritdoc/>
    [Reactive]
    public string? InputDirectory { get; set; }

    /// <inheritdoc/>
    [Reactive]
    public string? OutputDirectory { get; set; }

    /// <inheritdoc/>
    [Reactive]
    public bool Recursive { get; set; } = true;

    /// <inheritdoc/>
    [Reactive]
    public OperationMode SelectedMode { get; set; }

    /// <summary>
    /// Gets or sets the high level operation status.
    /// </summary>
    [Reactive]
    public string OverallStatus { get; set; } = string.Empty;

    /// <inheritdoc/>
    [Reactive]
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Default;

    /// <inheritdoc/>
    [Reactive]
    public ThreadCount ThreadCount { get; set; } = ThreadCount.Default;

    /// <inheritdoc/>
    [Reactive]
    public bool CopyFailedFiles { get; set; } = false;

    /// <inheritdoc/>
    public IEnumerable Modes { get; }

    public bool CanStop => this.CanStopHelper.Value;

    [Reactive]
    private StoppableTask? CurrentTask { get; set; }

    private ObservableAsPropertyHelper<bool> CanStopHelper { get; }

    private async Task ExecuteCompressAsync()
    {
        this.OverallStatus = string.Empty;
        try
        {
            using var cts = new CancellationTokenSource();
            var compressorTask = Core(cts.Token);
            using var x = Disposable.Create(() => this.CurrentTask = null);
            this.CurrentTask = new StoppableTask(compressorTask, cts);

            await compressorTask;
            this.OverallStatus = "Finished";
            this.logger.LogInformation("Run finished normally.");
        }
        catch (OperationCanceledException ex)
        {
            this.OverallStatus = ex.Message;
            this.logger.LogWarning("Run canceled.");
        }
        catch (Exception ex)
        {
            this.OverallStatus = $"⚠ {ex.Message}";
            this.logger.LogError(ex, "Run failed: {ex}", ex);
        }

        Task Core(CancellationToken token)
        {
            var parameters = this.CollectParameters();
            this.logger.LogInformation("Starting run: {parameters}", parameters);
            var run = this.compressor.PrepareNewRun(parameters, token);
            var result = run.Output.ToTask();
            run.Start();
            return result;
        }
    }

    private async Task ExecuteStopAsync()
    {
        var current = this.CurrentTask;
        if (current?.Task != null)
        {
            current.Stop.Cancel();
            try
            {
                await current.Task;
            }
            catch
            {
                // exceptions handled elsewhere
            }
        }
    }

    private FolderCompressorParameters CollectParameters()
    {
        var inputDir = this.ValidateDirectory(this.InputDirectory, false, "Input folder");
        var outputDir = this.ValidateDirectory(this.OutputDirectory, true, "Output folder");
        var (compressionOptions, executionOptions) = this.CollectAdvancedCompressionOptions();
        return new FolderCompressorParameters(inputDir, outputDir, this.Recursive, this.SelectedMode.Value, executionOptions, compressionOptions);
    }

    private IDirectoryInfo ValidateDirectory(
        string? directory,
        bool createIfNecessary,
        string parameterName)
    {
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException($"{parameterName} is not set.");
        }

        var result = this.fileSystem.DirectoryInfo.New(directory);

        if (!result.Exists)
        {
            if (createIfNecessary)
            {
                result.Create();
            }
            else
            {
                throw new DirectoryNotFoundException($"Directory {directory} does not exist.");
            }
        }

        return result;
    }

    private (ProcessingOptions ProcessingOptions, ExecutionOptions ExecutionOptions) CollectAdvancedCompressionOptions()
    {
        ProcessingOptions processingOptions = new(this.CompressionLevel, this.CopyFailedFiles);
        ExecutionOptions executionOptions = new(this.ThreadCount);
        return (processingOptions, executionOptions);
    }

    private record StoppableTask(Task Task, CancellationTokenSource Stop);
}
