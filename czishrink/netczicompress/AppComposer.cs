// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress;

using System.IO.Abstractions;

using Microsoft.Extensions.Logging;

using netczicompress.Models;
using netczicompress.Models.Clipboard;
using netczicompress.ViewModels;
using netczicompress.ViewModels.Formatters;
using netczicompress.Views;

using ReactiveUI;

/// <summary>
/// Composes the application with 'Pure DI'.
/// </summary>
public class AppComposer
{
    public static MainWindow ComposeMainWindow()
    {
        var info = new ProgramNameAndVersion();
        var mainViewModel = ComposeMainViewModel(info);

        return new MainWindow
        {
            DataContext = mainViewModel,
            Title = info.Name,
        };
    }

    public static MainViewModel ComposeMainViewModel(IProgramNameAndVersion info)
    {
        // Parameters that we might have to tweak
        const int MaxNumberOfErrorsToShow = 1000;
        const int ErrorBufferSize = 100;
        var errorBufferInterval = TimeSpan.FromSeconds(1);

        var statisticsReportingInterval = TimeSpan.FromSeconds(1);

        // Actual composition code
        var fs = new FileSystem();
        string libraryNameAndVersion = PInvokeFileProcessor.GetLibFullName();

        ILoggerFactory loggerfactory = new TraceLoggerFactory();
        var logger = loggerfactory.CreateLogger<App>();
        logger.LogInformation("Starting {CZI Shrink} using {libczicompressc}", info, libraryNameAndVersion);

        var noOp = new NoOpFileProcessor();
        IFileProcessor CreateFileProcessor(CompressionMode mode, ProcessingOptions options) => mode switch
        {
            CompressionMode.NoOp => noOp,
            _ => PInvokeFileProcessor.Create(mode, options),
        };
        IFolderCompressor compressor = new MultiThreadedFolderCompressor(CreateFileProcessor, new FileProcessingFailedHandler());

        IFileLauncher fileLauncher = new FileLauncher(fs, Environment.GetEnvironmentVariable);
        var gui = RxApp.MainThreadScheduler;
        var errorList = new ErrorListViewModel(
            maxNumberOfErrorsToShow: MaxNumberOfErrorsToShow,
            bufferInterval: errorBufferInterval,
            bufferCapacity: ErrorBufferSize,
            scheduler: gui,
            launcher: fileLauncher);
        var currentTasks = new CurrentTasksViewModel(scheduler: gui);
        var logViewModel = new LogFileViewModel(new CsvLoggingStrategy(fileLauncher), gui);

        var aggregateIndicationViewModel = new AggregateIndicationViewModel();
        var aggregateStatisticsViewModel = new AggregateStatisticsViewModel(
            aggregateIndicationViewModel: aggregateIndicationViewModel,
            ClipboardHelper.Instance,
            logger: loggerfactory.CreateLogger<AggregateStatisticsViewModel>(),
            timeSpanFormatter: new HumanReadableTimeSpanFormatter())
        {
            OpenLogFileCommand = logViewModel.OpenLogFileCommand,
        };

        var statisticsObserver = CompositeObserver.Create(aggregateStatisticsViewModel, aggregateIndicationViewModel);
        var statisticsRunObserver = new StatisticsRunObserver(statisticsObserver, statisticsReportingInterval, gui);

        compressor = compressor.DecorateWithRunObserver(statisticsRunObserver);
        compressor = compressor.DecorateWithRunObserver(logViewModel);
        compressor = compressor.DecorateWithRunObserver(errorList);
        compressor = compressor.DecorateWithObserver(currentTasks, gui);

        var mainViewModel = new MainViewModel(
                compressor: compressor,
                fileSystem: fs,
                errorList: errorList,
                currentTasksViewModel: currentTasks,
                aggregateStatisticsViewModel: aggregateStatisticsViewModel,
                aboutViewModel: new AboutViewModel(fileLauncher, info) { LibraryName = libraryNameAndVersion },
                logger: loggerfactory.CreateLogger<MainViewModel>());

        return mainViewModel;
    }
}
