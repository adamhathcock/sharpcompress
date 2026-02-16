using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Xz;

internal static partial class MultiByteIntegers
{
    public static ulong ReadXZInteger(this BinaryReader reader, int MaxBytes = 9)
    {
        ThrowHelper.ThrowIfNegativeOrZero(MaxBytes);

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
}
