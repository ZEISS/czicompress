// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.Security.Cryptography;

/// <summary>Computes file integrity evidence with bounded, cancelable reads.</summary>
public static class FileIntegrity
{
    /// <summary>Hashes a stream, honoring cancellation through its final read.</summary>
    /// <param name="input">The readable stream.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The uppercase SHA-256 digest.</returns>
    public static string ComputeSha256(Stream input, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[64 * 1024];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int count = input.Read(buffer, 0, buffer.Length);
            token.ThrowIfCancellationRequested();
            if (count == 0)
            {
                return Convert.ToHexString(hash.GetHashAndReset());
            }

            hash.AppendData(buffer, 0, count);
        }
    }
}
