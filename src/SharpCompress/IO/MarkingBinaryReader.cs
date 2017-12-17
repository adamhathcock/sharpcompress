using System;
using System.IO;
using SharpCompress.Converters;

namespace SharpCompress.IO
{
    internal class MarkingBinaryReader : BinaryReader
    {
        public MarkingBinaryReader(Stream stream)
            : base(stream)
        {
        }

        public virtual long CurrentReadByteCount { get; protected set; }

        public virtual void Mark()
        {
            CurrentReadByteCount = 0;
        }

        public override int Read()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            throw new NotSupportedException();
        }

        public override int Read(char[] buffer, int index, int count)
        {
            throw new NotSupportedException();
        }

        public override bool ReadBoolean()
        {
            return ReadByte() != 0;
        }

        // NOTE: there is a somewhat fragile dependency on the internals of this class
        // with RarCrcBinaryReader and RarCryptoBinaryReader.
        //
        // RarCrcBinaryReader/RarCryptoBinaryReader need to override any specific methods
        // that call directly to the base BinaryReader and do not delegate to other methods
        // in this class so that it can track the each byte being read.
        //
        // if altering this class in a way that changes the implementation be sure to
        // update RarCrcBinaryReader/RarCryptoBinaryReader.
        public override byte ReadByte()
        {
            CurrentReadByteCount++;
            return base.ReadByte();
        }

        public override byte[] ReadBytes(int count)
        {
            CurrentReadByteCount += count;
            var bytes = base.ReadBytes(count);
            if (bytes.Length != count)
            {
                throw new EndOfStreamException(string.Format("Could not read the requested amount of bytes.  End of stream reached. Requested: {0} Read: {1}", count, bytes.Length));
            }
            return bytes;
        }

        public override char ReadChar()
        {
            throw new NotSupportedException();
        }

        public override char[] ReadChars(int count)
        {
            throw new NotSupportedException();
        }

#if !SILVERLIGHT
        public override decimal ReadDecimal()
        {
            throw new NotSupportedException();
        }
#endif

        public override double ReadDouble()
        {
            throw new NotSupportedException();
        }

        public override short ReadInt16()
        {
            return DataConverter.LittleEndian.GetInt16(ReadBytes(2), 0);
        }

        public override int ReadInt32()
        {
            return DataConverter.LittleEndian.GetInt32(ReadBytes(4), 0);
        }

        public override long ReadInt64()
        {
            return DataConverter.LittleEndian.GetInt64(ReadBytes(8), 0);
        }

        public override sbyte ReadSByte()
        {
            return (sbyte)ReadByte();
        }

        public override float ReadSingle()
        {
            throw new NotSupportedException();
        }

        public override string ReadString()
        {
            throw new NotSupportedException();
        }

        public override ushort ReadUInt16()
        {
            return DataConverter.LittleEndian.GetUInt16(ReadBytes(2), 0);
        }

        public override uint ReadUInt32()
        {
            return DataConverter.LittleEndian.GetUInt32(ReadBytes(4), 0);
        }

        public override ulong ReadUInt64()
        {
            return DataConverter.LittleEndian.GetUInt64(ReadBytes(8), 0);
        }

        // RAR5 style variable length encoded value
        // maximum value of 0xffffffffffffffff (64 bits)
        // implies max 10 bytes consumed
        //
        // Variable length integer. Can include one or more bytes, where lower 7 bits of every byte contain integer data
        // and highest bit in every byte is the continuation flag. If highest bit is 0, this is the last byte in sequence.
        // So first byte contains 7 least significant bits of integer and continuation flag. Second byte, if present,
        // contains next 7 bits and so on.
        public ulong ReadRarVInt() {
            int shift = 0;
            ulong result = 0;
            do {
                byte b0 = ReadByte();
                var b1 = b0 & 0x7f;
                ulong n = (ulong)b1;
                result |= n << shift;
                if (b0 == b1) {
                    return result;
                }
                // note: we're actually only allowing a max high bit of 2^56
                // to avoid an extra complex check for shift overflow due to the
                // 10th byte having high bits set
                shift += 7;
            } while (shift < 63);

            throw new FormatException("malformed vint");
        }

        public uint ReadRarVIntUInt32(int maxBytes = 5) {
            int shift = 0;
            uint result = 0;
            do {
                byte b0 = ReadByte();
                var b1 = b0 & 0x7f;
                uint n = (uint)b1;
                result |= n << shift;
                if (b0 == b1) {
                    return result;
                }
                shift += 7;
                // NOTE: we are too strict here but handling the full range adds complexity and we don't need it
            } while (shift < 28 && --maxBytes > 0);

            throw new FormatException("malformed vint");
        }

        public ushort ReadRarVIntUInt16(int maxBytes = 3) {
            int shift = 0;
            uint result = 0;
            do {
                byte b0 = ReadByte();
                var b1 = b0 & 0x7f;
                uint n = (uint)b1;
                result |= n << shift;
                if (b0 == b1) {
                    return checked((ushort)result);
                }
                shift += 7;
                // NOTE: we are too strict here but handling the full range adds complexity and we don't need it
            } while (shift < 14 && --maxBytes > 0);

            throw new FormatException("malformed vint");
        }

        public byte ReadRarVIntByte(int maxBytes = 1) {
            int shift = 0;
            uint result = 0;
            do {
                byte b0 = ReadByte();
                var b1 = b0 & 0x7f;
                uint n = (uint)b1;
                result |= n << shift;
                if (b0 == b1) {
                    return checked((byte)result);
                }
                shift += 7;
                // NOTE: we are too strict here but handling the full range adds complexity and we don't need it
            } while (shift < 7 && --maxBytes > 0);

            throw new FormatException("malformed vint");
        }
    }
}