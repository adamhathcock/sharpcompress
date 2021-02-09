#nullable disable

using System;
using System.IO;

namespace SharpCompress.Crypto
{
    internal sealed class Crc32Stream : Stream
    {
        public const uint DefaultPolynomial = 0xedb88320u;
        public const uint DefaultSeed = 0xffffffffu;

        private static uint[] defaultTable;

        private readonly uint[] table;
        private uint hash;

        private readonly Stream stream;

        public Crc32Stream(Stream stream)
            : this(stream, DefaultPolynomial, DefaultSeed)
        {
        }

        public Crc32Stream(Stream stream, uint polynomial, uint seed)
        {
            this.stream = stream;
            table = InitializeTable(polynomial);
            hash = seed;
        }

        public Stream WrappedStream => stream;

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

#if !NET461 && !NETSTANDARD2_0

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            stream.Write(buffer);

            hash = CalculateCrc(table, hash, buffer);
        }
#endif

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
            hash = CalculateCrc(table, hash, buffer.AsSpan(offset, count));
        }

        public override void WriteByte(byte value)
        {
            stream.WriteByte(value);
            hash = CalculateCrc(table, hash, value);
        }

        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public uint Crc => ~hash;

        public static uint Compute(byte[] buffer)
        {
            return Compute(DefaultSeed, buffer);
        }

        public static uint Compute(uint seed, byte[] buffer)
        {
            return Compute(DefaultPolynomial, seed, buffer);
        }

        public static uint Compute(uint polynomial, uint seed, ReadOnlySpan<byte> buffer)
        {
            return ~CalculateCrc(InitializeTable(polynomial), seed, buffer);
        }

        private static uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null)
            {
                return defaultTable;
            }

            var createTable = new uint[256];
            for (var i = 0; i < 256; i++)
            {
                var entry = (uint)i;
                for (var j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                    {
                        entry = (entry >> 1) ^ polynomial;
                    }
                    else
                    {
                        entry = entry >> 1;
                    }
                }

                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
            {
                defaultTable = createTable;
            }

            return createTable;
        }

        private static uint CalculateCrc(uint[] table, uint crc, ReadOnlySpan<byte> buffer)
        {
            unchecked
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    crc = CalculateCrc(table, crc, buffer[i]);
                }
            }
            return crc;
        }

        private static uint CalculateCrc(uint[] table, uint crc, byte b)
        {
            return (crc >> 8) ^ table[(crc ^ b) & 0xFF];
        }
    }
}
