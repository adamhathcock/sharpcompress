using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class RarHeader : IRarHeader
    {
        protected readonly bool isRar5;

        protected RarHeader() { 
            //TODO
            //throw new NotImplementedException();
        }

        private RarHeader(bool isRar5) 
        {
            this.isRar5 = isRar5;
        }

        protected RarHeader(RarHeader header, RarCrcBinaryReader reader) {
            this.isRar5 = header.isRar5;
            FillBase(header);
            ReadFromReader(reader);
            ReadBytes = reader.CurrentReadByteCount;

            int headerSizeDiff = HeaderSize - (int)ReadBytes;
            if (headerSizeDiff > 0)
            {
                reader.ReadBytes(headerSizeDiff);
            }

            VerifyHeaderCrc(reader.GetCrc32());
        }

        private void FillBase(RarHeader baseHeader)
        {
            HeadCRC = baseHeader.HeadCRC;
            HeaderType = baseHeader.HeaderType;
            Flags = baseHeader.Flags;
            HeaderSize = baseHeader.HeaderSize;
            ExtraSize = baseHeader.ExtraSize;
            AdditionalDataSize = baseHeader.AdditionalDataSize;
            ReadBytes = baseHeader.ReadBytes;
            ArchiveEncoding = baseHeader.ArchiveEncoding;
        }

        internal static RarHeader ReadBase(RarCrcBinaryReader reader, ArchiveEncoding archiveEncoding, bool isRar5)
        {
            try
            {
                var header = new RarHeader(isRar5);
                header.ArchiveEncoding = archiveEncoding;
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
            if (this.isRar5) 
            {
                HeadCRC = reader.ReadUInt32();
                reader.ResetCrc();
                HeaderSize = (int)reader.ReadRarVIntUInt32(3);
                reader.Mark();
                HeaderType = (HeaderType)reader.ReadRarVIntUInt32(1);
                Flags = reader.ReadRarVIntUInt16(2);
                if ((Flags & HeaderFlagsV5.HasExtra) == HeaderFlagsV5.HasExtra)
                {
                    ExtraSize = reader.ReadRarVIntUInt32();
                }
                if ((Flags & HeaderFlagsV5.HasData) == HeaderFlagsV5.HasData)
                {
                    AdditionalDataSize = reader.ReadRarVIntUInt32();
                }
            } else {
                reader.Mark();
                HeadCRC = reader.ReadUInt16();
                reader.ResetCrc();
                HeaderType = (HeaderType)reader.ReadByte();
                Flags = reader.ReadUInt16();
                HeaderSize = reader.ReadInt16();
                if ((Flags & HeaderFlagsV4.HasData) == HeaderFlagsV4.HasData)
                {
                    AdditionalDataSize = reader.ReadUInt32();
                }
            }
        }

        protected virtual void ReadFromReader(MarkingBinaryReader reader)
        {
            throw new NotImplementedException();
        }
//x
        internal T PromoteHeader<T>(RarCrcBinaryReader reader)
            where T : RarHeader, new()
        {
            T header = new T();
            header.FillBase(this);
            header.ReadFromReader(reader);
            header.ReadBytes = reader.CurrentReadByteCount;

            int headerSizeDiff = header.HeaderSize - (int)header.ReadBytes;
            if (headerSizeDiff > 0)
            {
                reader.ReadBytes(headerSizeDiff);
            }

            VerifyHeaderCrc(reader.GetCrc32());

            return header;
        }

        private void VerifyHeaderCrc(uint crc32)
        {
            if (HeaderType != HeaderType.MarkHeader)
            {
                var b = (this.isRar5 ? crc32 : (ushort)crc32) == HeadCRC;
                if (!b)
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

        protected uint HeadCRC { get; private set; }

        public HeaderType HeaderType { get; private set; }

        /// <summary>
        /// Untyped flags.  These should be typed when Promoting to another header
        /// </summary>
        protected ushort Flags { get; private set; }

        protected int HeaderSize { get; private set; }

        internal ArchiveEncoding ArchiveEncoding { get; private set; }

        /// <summary>
        /// Extra header data.
        /// </summary>
        protected uint ExtraSize { get; private set; }

        /// <summary>
        /// Additional data size, eg recovery record
        /// </summary>
        protected uint AdditionalDataSize { get; private set; }
    }
}