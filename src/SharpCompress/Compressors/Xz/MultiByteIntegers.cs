using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Xz;

internal static class MultiByteIntegers
{
    public static ulong ReadXZInteger(this BinaryReader reader, int MaxBytes = 9)
    {
        if (MaxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBytes));
        }

        if (MaxBytes > 9)
        {
            MaxBytes = 9;
        }

        var LastByte = reader.ReadByte();
        var Output = (ulong)LastByte & 0x7F;

        var i = 0;
        while ((LastByte & 0x80) != 0)
        {
            if (++i >= MaxBytes)
            {
                throw new InvalidFormatException();
            }

            LastByte = reader.ReadByte();
            if (LastByte == 0)
            {
                throw new InvalidFormatException();
            }

            Output |= ((ulong)(LastByte & 0x7F)) << (i * 7);
        }
        return Output;
    }

    public static async Task<ulong> ReadXZIntegerAsync(
        this BinaryReader reader,
        CancellationToken cancellationToken = default,
        int MaxBytes = 9
    )
    {
        if (MaxBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxBytes));
        }

        if (MaxBytes > 9)
        {
            MaxBytes = 9;
        }

        var LastByte = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        var Output = (ulong)LastByte & 0x7F;

        var i = 0;
        while ((LastByte & 0x80) != 0)
        {
            if (++i >= MaxBytes)
            {
                throw new InvalidFormatException();
            }

            LastByte = await reader.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (LastByte == 0)
            {
                throw new InvalidFormatException();
            }

            Output |= ((ulong)(LastByte & 0x7F)) << (i * 7);
        }
        return Output;
    }
}
