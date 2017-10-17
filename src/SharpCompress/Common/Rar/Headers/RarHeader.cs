using System;
using System.IO;
using SharpCompress.IO;
using System.Text;

namespace SharpCompress.Common.Rar.Headers
{
    internal class RarHeader
    {
        internal const short BaseBlockSize = 7;
        internal const short LONG_BLOCK = -0x8000;

        private void FillBase(RarHeader baseHeader)
        {
            HeadCRC = baseHeader.HeadCRC;
            HeaderType = baseHeader.HeaderType;
            Flags = baseHeader.Flags;
            HeaderSize = baseHeader.HeaderSize;
            AdditionalSize = baseHeader.AdditionalSize;
            ReadBytes = baseHeader.ReadBytes;
            ArchiveEncoding = baseHeader.ArchiveEncoding;
        }

        internal static RarHeader Create(RarCrcBinaryReader reader, ArchiveEncoding archiveEncoding)
        {
            try
            {
                RarHeader header = new RarHeader();

                header.ArchiveEncoding = archiveEncoding;
                reader.Mark();
                header.ReadStartFromReader(reader);
                header.ReadBytes += reader.CurrentReadByteCount;

                return header;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private void ReadStartFromReader(RarCrcBinaryReader reader)
        {
            HeadCRC = reader.ReadUInt16();
            reader.ResetCrc();
            HeaderType = (HeaderType)(reader.ReadByte() & 0xff);
            Flags = reader.ReadInt16();
            HeaderSize = reader.ReadInt16();
            if (FlagUtility.HasFlag(Flags, LONG_BLOCK))
            {
                AdditionalSize = reader.ReadUInt32();
            }
        }

        protected virtual void ReadFromReader(MarkingBinaryReader reader)
        {
            throw new NotImplementedException();
        }

        internal T PromoteHeader<T>(RarCrcBinaryReader reader)
            where T : RarHeader, new()
        {
            T header = new T();
            header.FillBase(this);

            reader.Mark();
            header.ReadFromReader(reader);
            header.ReadBytes += reader.CurrentReadByteCount;

            int headerSizeDiff = header.HeaderSize - (int)header.ReadBytes;

            if (headerSizeDiff > 0)
            {
                reader.ReadBytes(headerSizeDiff);
            }

            VerifyHeaderCrc(reader.GetCrc());

            return header;
        }

        private void VerifyHeaderCrc(ushort crc)
        {
            if (HeaderType != HeaderType.MarkHeader)
            {
                if (crc != HeadCRC)
                {
                    throw new InvalidFormatException("rar header crc mismatch");
                }
            }
        }

        protected virtual void PostReadingBytes(MarkingBinaryReader reader)
        {
        }

        /// <summary>
        /// This is the number of bytes read when reading the header
        /// </summary>
        protected long ReadBytes { get; private set; }

        protected ushort HeadCRC { get; private set; }

        internal HeaderType HeaderType { get; private set; }

        /// <summary>
        /// Untyped flags.  These should be typed when Promoting to another header
        /// </summary>
        protected short Flags { get; private set; }

        protected short HeaderSize { get; private set; }

        internal ArchiveEncoding ArchiveEncoding { get; private set; }

        /// <summary>
        /// This additional size of the header could be file data
        /// </summary>
        protected uint AdditionalSize { get; private set; }
    }
}