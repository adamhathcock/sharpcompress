using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class RarHeader : IRarHeader
    {
        private readonly HeaderType headerType;
        private readonly bool isRar5;

        private RarHeader(bool isRar5) 
        {
            this.headerType = HeaderType.Null;
            this.isRar5 = isRar5;
        }

        protected RarHeader(RarHeader header, RarCrcBinaryReader reader, HeaderType headerType) {
            this.headerType = headerType;
            this.isRar5 = header.IsRar5;
            HeaderCrc = header.HeaderCrc;
            HeaderCode = header.HeaderCode;
            HeaderFlags = header.HeaderFlags;
            HeaderSize = header.HeaderSize;
            ExtraSize = header.ExtraSize;
            AdditionalDataSize = header.AdditionalDataSize;
            ArchiveEncoding = header.ArchiveEncoding;
            ReadFinish(reader);
            ReadBytes = reader.CurrentReadByteCount;

            int headerSizeDiff = HeaderSize - (int)ReadBytes;
            if (headerSizeDiff > 0)
            {
                reader.ReadBytes(headerSizeDiff);
            }

            VerifyHeaderCrc(reader.GetCrc32());
        }

        protected virtual void ReadFinish(MarkingBinaryReader reader)
        {
            throw new NotImplementedException();
        }

        internal static RarHeader TryReadBase(RarCrcBinaryReader reader, ArchiveEncoding archiveEncoding, bool isRar5)
        {
            try
            {
                var header = new RarHeader(isRar5);
                header.ArchiveEncoding = archiveEncoding;
                header.ReadStart(reader);
                header.ReadBytes += reader.CurrentReadByteCount;
                return header;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private void ReadStart(RarCrcBinaryReader reader)
        {
            if (IsRar5) 
            {
                HeaderCrc = reader.ReadUInt32();
                reader.ResetCrc();
                HeaderSize = (int)reader.ReadRarVIntUInt32(3);
                reader.Mark();
                HeaderCode = reader.ReadRarVIntByte();
                HeaderFlags = reader.ReadRarVIntUInt16(2);
                if ((HeaderFlags & HeaderFlagsV5.HasExtra) == HeaderFlagsV5.HasExtra)
                {
                    ExtraSize = reader.ReadRarVIntUInt32();
                }
                if ((HeaderFlags & HeaderFlagsV5.HasData) == HeaderFlagsV5.HasData)
                {
                    AdditionalDataSize = (long)reader.ReadRarVInt();
                }
            } else {
                reader.Mark();
                HeaderCrc = reader.ReadUInt16();
                reader.ResetCrc();
                HeaderCode = reader.ReadByte();
                HeaderFlags = reader.ReadUInt16();
                HeaderSize = reader.ReadInt16();
                if ((HeaderFlags & HeaderFlagsV4.HasData) == HeaderFlagsV4.HasData)
                {
                    AdditionalDataSize = reader.ReadUInt32();
                }
            }
        }

        private void VerifyHeaderCrc(uint crc32)
        {
            var b = (IsRar5 ? crc32 : (ushort)crc32) == HeaderCrc;
            if (!b)
            {
                throw new InvalidFormatException("rar header crc mismatch");
            }
        }

        public HeaderType HeaderType { get { return this.headerType; } }

        protected bool IsRar5 { get { return this.isRar5; } }

        /// <summary>
        /// This is the number of bytes read when reading the header
        /// </summary>
        protected long ReadBytes { get; private set; }

        protected uint HeaderCrc { get; private set; }

        internal byte HeaderCode { get; private set; }

        protected ushort HeaderFlags { get; private set; }

        protected int HeaderSize { get; private set; }

        internal ArchiveEncoding ArchiveEncoding { get; private set; }

        /// <summary>
        /// Extra header size.
        /// </summary>
        protected uint ExtraSize { get; private set; }

        /// <summary>
        /// Size of additional data (eg file contents)
        /// </summary>
        protected long AdditionalDataSize { get; private set; }
    }
}