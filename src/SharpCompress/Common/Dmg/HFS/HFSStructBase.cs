using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SharpCompress.Common.Dmg.HFS
{
    internal abstract class HFSStructBase
    {
        private const int StringSize = 510;
        private const int OSTypeSize = 4;
        private static readonly DateTime Epoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly byte[] _buffer = new byte[StringSize];

        protected static byte ReadUInt8(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(byte)) != sizeof(byte))
                throw new EndOfStreamException();

            return _buffer[0];
        }

        protected static ushort ReadUInt16(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(ushort)) != sizeof(ushort))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadUInt16BigEndian(_buffer);
        }

        protected static short ReadInt16(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(short)) != sizeof(short))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadInt16BigEndian(_buffer);
        }

        protected static uint ReadUInt32(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(uint)) != sizeof(uint))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadUInt32BigEndian(_buffer);
        }

        protected static int ReadInt32(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(int)) != sizeof(int))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadInt32BigEndian(_buffer);
        }

        protected static ulong ReadUInt64(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(ulong)) != sizeof(ulong))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadUInt64BigEndian(_buffer);
        }

        protected static long ReadInt64(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(long)) != sizeof(long))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadInt64BigEndian(_buffer);
        }

        protected static string ReadString(Stream stream)
        {
            ushort length = ReadUInt16(stream);
            if (stream.Read(_buffer, 0, StringSize) != StringSize)
                throw new EndOfStreamException();
            return Encoding.Unicode.GetString(_buffer, 0, Math.Min(length * 2, StringSize));
        }

        protected static DateTime ReadDate(Stream stream)
        {
            uint seconds = ReadUInt32(stream);
            var span = TimeSpan.FromSeconds(seconds);
            return Epoch + span;
        }

        protected static byte ReadUInt8(ref ReadOnlySpan<byte> data)
        {
            byte val = data[0];
            data = data.Slice(sizeof(byte));
            return val;
        }

        protected static ushort ReadUInt16(ref ReadOnlySpan<byte> data)
        {
            ushort val = BinaryPrimitives.ReadUInt16BigEndian(data);
            data = data.Slice(sizeof(ushort));
            return val;
        }

        protected static short ReadInt16(ref ReadOnlySpan<byte> data)
        {
            short val = BinaryPrimitives.ReadInt16BigEndian(data);
            data = data.Slice(sizeof(short));
            return val;
        }

        protected static uint ReadUInt32(ref ReadOnlySpan<byte> data)
        {
            uint val = BinaryPrimitives.ReadUInt32BigEndian(data);
            data = data.Slice(sizeof(uint));
            return val;
        }

        protected static int ReadInt32(ref ReadOnlySpan<byte> data)
        {
            int val = BinaryPrimitives.ReadInt32BigEndian(data);
            data = data.Slice(sizeof(int));
            return val;
        }

        protected static ulong ReadUInt64(ref ReadOnlySpan<byte> data)
        {
            ulong val = BinaryPrimitives.ReadUInt64BigEndian(data);
            data = data.Slice(sizeof(ulong));
            return val;
        }

        protected static long ReadInt64(ref ReadOnlySpan<byte> data)
        {
            long val = BinaryPrimitives.ReadInt64BigEndian(data);
            data = data.Slice(sizeof(long));
            return val;
        }

        protected static string ReadString(ref ReadOnlySpan<byte> data, bool truncate)
        {
            int length = ReadUInt16(ref data);
            if (truncate)
            {
                length = Math.Min(length * 2, StringSize);
                data.Slice(0, length).CopyTo(_buffer);
                data = data.Slice(length);
                return Encoding.BigEndianUnicode.GetString(_buffer, 0, length);
            }
            else
            {
                data.Slice(0, StringSize).CopyTo(_buffer);
                data = data.Slice(StringSize);
                return Encoding.BigEndianUnicode.GetString(_buffer, 0, Math.Min(length * 2, StringSize));
            }
        }

        protected static DateTime ReadDate(ref ReadOnlySpan<byte> data)
        {
            uint seconds = ReadUInt32(ref data);
            var span = TimeSpan.FromSeconds(seconds);
            return Epoch + span;
        }

        protected static string ReadOSType(ref ReadOnlySpan<byte> data)
        {
            data.Slice(0, OSTypeSize).CopyTo(_buffer);
            data = data.Slice(OSTypeSize);
            return Encoding.ASCII.GetString(_buffer, 0, OSTypeSize).NullTerminate();
        }

        protected static HFSPoint ReadPoint(ref ReadOnlySpan<byte> data)
        {
            return new HFSPoint()
            {
                V = ReadInt16(ref data),
                H = ReadInt16(ref data)
            };
        }

        protected static HFSRect ReadRect(ref ReadOnlySpan<byte> data)
        {
            return new HFSRect()
            {
                Top = ReadInt16(ref data),
                Left = ReadInt16(ref data),
                Bottom = ReadInt16(ref data),
                Right = ReadInt16(ref data)
            };
        }
    }
}
