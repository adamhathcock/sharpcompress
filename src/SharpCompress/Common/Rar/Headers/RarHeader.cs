using System;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class RarHeader : IRarHeader
    {
        private readonly HeaderType headerType;
        private readonly bool isRar5;

        internal static RarHeader TryReadBase(RarCrcBinaryReader reader, bool isRar5, ArchiveEncoding archiveEncoding)
        {
            try
            {
                return new RarHeader(reader, isRar5, archiveEncoding);
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private RarHeader(RarCrcBinaryReader reader, bool isRar5, ArchiveEncoding archiveEncoding) 
        {
            this.headerType = HeaderType.Null;
            this.isRar5 = isRar5;
            ArchiveEncoding = archiveEncoding;
            if (IsRar5) 
            {
                HeaderCrc = reader.ReadUInt32();
                reader.ResetCrc();
                HeaderSize = (int)reader.ReadRarVIntUInt32(3);
                reader.Mark();
                HeaderCode = reader.ReadRarVIntByte();
                HeaderFlags = reader.ReadRarVIntUInt16(2);
                if (HasHeaderFlag(HeaderFlagsV5.HasExtra))
                {
                    ExtraSize = reader.ReadRarVIntUInt32();
                }
                if (HasHeaderFlag(HeaderFlagsV5.HasData))
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
                if (HasHeaderFlag(HeaderFlagsV4.HasData))
                {
                    AdditionalDataSize = reader.ReadUInt32();
                }
            }
            ReadBytes = reader.CurrentReadByteCount;
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
        protected long ReadBytes { get; }

        protected uint HeaderCrc { get; }

        internal byte HeaderCode { get; }

        protected ushort HeaderFlags { get; }

        private bool HasHeaderFlag(ushort flag) 
        {
            return (HeaderFlags & flag) == flag;
        }

        protected int HeaderSize { get; }

        internal ArchiveEncoding ArchiveEncoding { get; }

        /// <summary>
        /// Extra header size.
        /// </summary>
        protected uint ExtraSize { get; }

        /// <summary>
        /// Size of additional data (eg file contents)
        /// </summary>
        protected long AdditionalDataSize { get; }
    }
}