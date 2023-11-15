// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using netczicompress.Models;
using netczicompress.Models.Clipboard;
using netczicompress.ViewModels.Converters;
using ReactiveUI;

/// <summary>
/// This is the view-model used for displaying Aggregate Statistics.
/// This information is "for the compression as a whole", and not for individual files.
/// </summary>
public class AggregateStatisticsViewModel : ViewModelBase, IObserver<AggregateStatistics>, IAggregateStatisticsViewModel
{
    private static readonly AggregateStatistics InitialAggregateStatistics = AggregateStatistics.Empty;

    private readonly IClipboardHelper? clipboardHelper;
    private readonly ILogger logger;

    private AggregateStatistics current;

    public AggregateStatisticsViewModel(IAggregateIndicationViewModel aggregateIndicationViewModel, IClipboardHelper? clipboardHelper, ILogger<AggregateStatisticsViewModel> logger)
    {
        this.AggregateIndicationViewModel = aggregateIndicationViewModel;
        this.clipboardHelper = clipboardHelper;
        this.logger = logger;
        this.current = InitialAggregateStatistics;

        // construct an expression for "when to enable the Copy Badge"-button - we bind the enabled-status
        //  to the "AggregateIndicationViewModel.IndicationStatus" property.
        var copyBadgeCanExecuteExpression = aggregateIndicationViewModel.
                                                WhenAnyValue(x => x.IndicationStatus).
                                                Select(item => item is AggregateIndicationStatus.Success or AggregateIndicationStatus.Error);
        this.CopyBadgeToClipboardCommand = ReactiveCommand.Create(
            execute: this.DoCopyBadgeToClipboard,
            canExecute: copyBadgeCanExecuteExpression);
    }

    public event Action? BadgeCopiedToClipboardRaised;

    public int FilesWithErrors => this.current.FilesWithErrors;

    public int FilesWithNoErrors => this.current.FilesWithNoErrors;

    public TimeSpan Duration => this.current.Duration;

    public long InputBytes => this.current.InputBytes;

    public long OutputBytes => this.current.OutputBytes;

    public long DeltaBytes => this.current.DeltaBytes;

    public float OutputToInputRatio => this.current.OutputToInputRatio;

    public required ICommand OpenLogFileCommand { get; init; }

    public ICommand CopyBadgeToClipboardCommand { get; private init; }

    public IAggregateIndicationViewModel AggregateIndicationViewModel { get; }

    public bool IsCopyBadgeToClipboardCommandVisible => ClipboardHelper.Instance != null;

    void IObserver<AggregateStatistics>.OnCompleted()
    {
    }

    void IObserver<AggregateStatistics>.OnError(Exception error)
    {
    }

    public void OnNext(AggregateStatistics value)
    {
        this.current = value;
        this.RaisePropertyChanged(string.Empty);
    }

    private void DoCopyBadgeToClipboard()
    {
        if (this.clipboardHelper != null)
        {
            var control = new netczicompress.Views.CompressionResultBadge();

            control.FilesWithoutErrorsValue.Text = string.Format(CultureInfo.CurrentCulture, @"{0:D}", this.FilesWithNoErrors);
            control.DurationValue.Text = string.Format(CultureInfo.CurrentCulture, @"{0:hh\:mm\.ss}", this.Duration);
            control.InputSizeValue.Text = new BytesToStringConverter().Convert(this.InputBytes, typeof(long), null, CultureInfo.CurrentCulture).ToString();
            control.OutputSizeValue.Text = new BytesToStringConverter().Convert(this.OutputBytes, typeof(long), null, CultureInfo.CurrentCulture).ToString();
            control.DeltaSizeValue.Text = new BytesToStringConverter().Convert(this.DeltaBytes, typeof(long), null, CultureInfo.CurrentCulture).ToString();
            control.OutputInputRatioValue.Text = new FloatToCompressionRatioStringConverter().Convert(this.OutputToInputRatio, typeof(float), null, CultureInfo.CurrentCulture).ToString();

            // call measure once with infinity so that we get the "desired size" populated
            control.Measure(Size.Infinity);
            var size = control.DesiredSize;
            int width = (int)size.Width;
            int height = (int)size.Height;
            size = new Size(width, height);

            control.Measure(size);
            control.Arrange(new Rect(size));

            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
            bitmap.Render(control);
            try
            {
                this.clipboardHelper.PutBitmapIntoClipboard(bitmap);
                this.BadgeCopiedToClipboardRaised?.Invoke();
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "putting badge into clipboard failed: {ex}", exception);
            }
        }
    }
}
