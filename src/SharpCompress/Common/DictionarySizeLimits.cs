using System;
using System.Threading;

namespace SharpCompress.Common;

/// <summary>
/// Process-wide dictionary/window and PPMd model size limits, in bytes.
/// Configure once at application startup, not for individual extraction requests.
/// These limits do not bound total memory, allocator overhead, or decompressed output.
/// </summary>
public static class DictionarySizeLimits
{
    private static long rar4 = 4L << 20;
    private static long rar5 = 64L << 20;
    private static long lzma = 64L << 20;
    private static long lzma2 = 64L << 20;
    private static long ppmd = 64L << 20;
    private static long zStandard = 128L << 20;

    /// <summary>RAR4 window limit. Defaults to the format maximum of 4 MiB.</summary>
    public static long Rar4
    {
        get => Interlocked.Read(ref rar4);
        set => SetMaximum(ref rar4, value, nameof(Rar4));
    }

    /// <summary>RAR5 window limit. Defaults to 64 MiB.</summary>
    public static long Rar5
    {
        get => Interlocked.Read(ref rar5);
        set => SetMaximum(ref rar5, value, nameof(Rar5));
    }

    /// <summary>LZMA dictionary limit, including LZip, ZIP, and 7z. Defaults to 64 MiB.</summary>
    public static long Lzma
    {
        get => Interlocked.Read(ref lzma);
        set => SetMaximum(ref lzma, value, nameof(Lzma));
    }

    /// <summary>LZMA2 dictionary limit, including XZ and 7z. Defaults to 64 MiB.</summary>
    public static long Lzma2
    {
        get => Interlocked.Read(ref lzma2);
        set => SetMaximum(ref lzma2, value, nameof(Lzma2));
    }

    /// <summary>PPMd model limit, including ZIP, 7z, and RAR models. Defaults to 64 MiB.</summary>
    public static long Ppmd
    {
        get => Interlocked.Read(ref ppmd);
        set => SetMaximum(ref ppmd, value, nameof(Ppmd));
    }

    /// <summary>
    /// Zstandard streaming window limit. Defaults to 128 MiB.
    /// Must be between 1 KiB and 1 GiB on 32-bit processes or 2 GiB on 64-bit processes.
    /// </summary>
    public static long ZStandard
    {
        get => Interlocked.Read(ref zStandard);
        set
        {
            // Match the engine bounds before replacing a working process-wide configuration.
            var maximum = 1L << (IntPtr.Size == 4 ? 30 : 31);
            if (value < 1024 || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ZStandard),
                    $"Zstandard window limits must be between 1024 and {maximum} bytes."
                );
            }
            SetMaximum(ref zStandard, value, nameof(ZStandard));
        }
    }

    private static void SetMaximum(ref long field, long value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "Dictionary size limits must be positive.");
        }
        Interlocked.Exchange(ref field, value);
    }

    internal static void Validate(long size, long maximum, string format)
    {
        if (size <= 0 || size > maximum)
        {
            throw new InvalidFormatException(
                $"{format} dictionary size {size} is invalid or exceeds the maximum of {maximum} bytes."
            );
        }
    }
}
