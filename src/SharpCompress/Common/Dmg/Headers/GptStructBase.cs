using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace SharpCompress.Common.Dmg.Headers
{
    internal abstract class GptStructBase
    {
        private static readonly byte[] _buffer = new byte[8];

        protected static ushort ReadUInt16(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(ushort)) != sizeof(ushort))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadUInt16LittleEndian(_buffer);
        }

        protected static uint ReadUInt32(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(uint)) != sizeof(uint))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadUInt32LittleEndian(_buffer);
        }

        protected static ulong ReadUInt64(Stream stream)
        {
            if (stream.Read(_buffer, 0, sizeof(ulong)) != sizeof(ulong))
                throw new EndOfStreamException();

            return BinaryPrimitives.ReadUInt64LittleEndian(_buffer);
        }

        protected static Guid ReadGuid(Stream stream)
        {
            int a = (int)ReadUInt32(stream);
            short b = (short)ReadUInt16(stream);
            short c = (short)ReadUInt16(stream);

            if (stream.Read(_buffer, 0, 8) != 8)
                throw new EndOfStreamException();

            return new Guid(a, b, c, _buffer);
        }

        protected static string ReadString(Stream stream, int byteSize)
        {
            var buffer = new byte[byteSize];
            if (stream.Read(buffer, 0, byteSize) != byteSize)
                throw new EndOfStreamException();
            return Encoding.Unicode.GetString(buffer).NullTerminate();
        }
    }
}
