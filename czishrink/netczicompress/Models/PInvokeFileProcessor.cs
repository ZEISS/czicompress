// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

/// <summary>
/// An <see cref="IFileProcessor"/> that uses the native libczicompressc C API to do the actual work.
/// </summary>
public sealed partial class PInvokeFileProcessor : IFileProcessor
{
    private IntPtr nativeFileProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="PInvokeFileProcessor"/> class.
    /// </summary>
    /// <param name="mode">The compression mode to use, not all modes may be supported.</param>
    /// <param name="options">The processing mode to use.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="mode"/> is <see cref="CompressionMode.NoOp"/>.</exception>
    /// <exception cref="ApplicationException">Thrown when the native file processor cannot be created.</exception>
    public PInvokeFileProcessor(CompressionMode mode, ProcessingOptions options)
    {
        if (mode == CompressionMode.NoOp)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        NativeMethods.Command cmd = mode switch
        {
            CompressionMode.Decompress => NativeMethods.Command.Decompress,
            _ => NativeMethods.Command.Compress,
        };

        NativeMethods.CompressionStrategy strategy = mode switch
        {
            CompressionMode.CompressAll => NativeMethods.CompressionStrategy.All,
            CompressionMode.CompressUncompressedAndZstd =>
                NativeMethods.CompressionStrategy.UncompressedAndZStdCompressed,
            _ => NativeMethods.CompressionStrategy.OnlyUncompressed,
        };

        this.nativeFileProcessor = NativeMethods.CreateFileProcessor(cmd, strategy, options.CompressionLevel.Value);

        if (this.nativeFileProcessor == IntPtr.Zero)
        {
            throw new ApplicationException("Failed to create native file processor.");
        }
    }

    public bool NeedsExistingOutputDirectory => true;

    private bool IsDisposed
    {
        get => this.nativeFileProcessor == IntPtr.Zero;
    }

    public static IFileProcessor Create(CompressionMode mode, ProcessingOptions options)
    {
        return new PInvokeFileProcessor(mode, options);
    }

    public static string GetLibFullName() => NativeMethods.GetLibFullName();

    public void ProcessFile(string inputPath, string outputPath, ReportProgress progressReport, CancellationToken token)
    {
        this.CheckDisposed();

        var errorMessageBuffer = ArrayPool<byte>.Shared.Rent(1024);
        var errorMessageLength = errorMessageBuffer.Length;

        bool ReportProgressCheckCancelImpl(int progress)
        {
            progressReport(progress);

            return !token.IsCancellationRequested;
        }

        var result = NativeMethods.ProcessFile(
            this.nativeFileProcessor,
            inputPath,
            outputPath,
            errorMessageBuffer,
            ref errorMessageLength,
            ReportProgressCheckCancelImpl);

        GC.KeepAlive(this.nativeFileProcessor);
        GC.KeepAlive(errorMessageBuffer);
        GC.KeepAlive(errorMessageLength);
        GC.KeepAlive(progressReport);

        if (result != 0)
        {
            var message = Encoding.UTF8.GetString(errorMessageBuffer, 0, errorMessageLength);
            throw new IOException(message);
        }

        // Make sure that we don't report cancellation as success, even if the native lib returns 0 when canceled.
        token.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        if (!this.IsDisposed)
        {
            NativeMethods.DestroyFileProcessor(this.nativeFileProcessor);
        }

        this.nativeFileProcessor = IntPtr.Zero;
    }

    private void CheckDisposed()
    {
        if (this.IsDisposed)
        {
            throw new ObjectDisposedException(this.ToString());
        }
    }

    internal static partial class NativeMethods
    {
        private const string LibName = "libczicompressc";
        private const StringMarshalling LibStringEncoding = StringMarshalling.Utf8;

        static NativeMethods()
        {
            GetLibVersion(out int major, out int minor, out int patch);

            var actual = (major, minor);
            (int Major, int Minor) expected = (0, 5);

            bool isCompatible = major == 0
                ? actual == expected
                : major == expected.Major && minor >= expected.Minor;

            if (!isCompatible)
            {
                throw new InvalidOperationException($"Expecting {LibName} {nameof(GetLibVersion)} to be compatible with {expected} but found {actual}.");
            }

#if !DEBUG
            var fullName = GetLibFullName();
            if (fullName.Contains("DEBUG"))
            {
                throw new InvalidOperationException($"You must not use {fullName} in a Release build of {nameof(netczicompress)}. Use a Release build of the library.");
            }
#endif
        }

        /// <summary>
        /// Delegate that is invoked by processing function to provide updates.
        /// </summary>
        /// <param name="progressPercent">The progress in percent.</param>
        /// <returns>True if operation should continue; false if canceled.</returns>
        public delegate bool ReportProgressCheckCancel(int progressPercent);

        public enum Command
        {
            Compress = 1,
            Decompress = 2,
        }

        public enum CompressionStrategy
        {
            All = 1,
            OnlyUncompressed = 2,
            UncompressedAndZStdCompressed = 3,
        }

        public static string GetLibFullName(int suggestedBufferLength = 64)
        {
            checked
            {
                var buffer = ArrayPool<byte>.Shared.Rent(suggestedBufferLength);
                ulong bufferLength = (ulong)buffer.Length;
                if (NativeMethods.GetLibVersionString(buffer, ref bufferLength))
                {
                    var version = Encoding.UTF8.GetString(buffer, 0, buffer.Count(b => b != 0));
                    return $"{LibName} {version}";
                }
                else if (bufferLength > (ulong)suggestedBufferLength)
                {
                    // This means that the buffer was too small
                    return GetLibFullName((int)bufferLength);
                }
                else
                {
                    throw new Exception($"Failed to determine version of {LibName}");
                }
            }
        }

        [LibraryImport(LibName, StringMarshalling = LibStringEncoding)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static partial bool GetLibVersionString(byte[] buffer, ref ulong bufferLength);

        [LibraryImport(LibName, StringMarshalling = LibStringEncoding)]
        public static partial void GetLibVersion(out int major, out int minor, out int patch);

        [LibraryImport(LibName, StringMarshalling = LibStringEncoding)]
        public static partial IntPtr CreateFileProcessor(Command command, CompressionStrategy strategy, int compressionLevel);

        [LibraryImport(LibName, StringMarshalling = LibStringEncoding)]
        public static partial void DestroyFileProcessor(IntPtr file_processor);

        [LibraryImport(LibName, StringMarshalling = LibStringEncoding)]
        public static partial int ProcessFile(
            IntPtr file_processor,
            string input_path,
            string output_path,
            byte[] error_message,
            ref int error_message_length,
            ReportProgressCheckCancel reportProgress);
    }
}
