using System.IO;
using SharpCompress.IO;

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
        }

        internal static RarHeader Create(MarkingBinaryReader reader)
        {
            try
            {
                RarHeader header = new RarHeader();

                reader.Mark();
                header.ReadFromReader(reader);
                header.ReadBytes += reader.CurrentReadByteCount;

                return header;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        protected virtual void ReadFromReader(MarkingBinaryReader reader)
        {
            HeadCRC = reader.ReadInt16();
            HeaderType = (HeaderType)(int)(reader.ReadByte() & 0xff);
            Flags = reader.ReadInt16();
            HeaderSize = reader.ReadInt16();
            if (FlagUtility.HasFlag(Flags, LONG_BLOCK))
            {
                AdditionalSize = reader.ReadUInt32();
            }
        }

        internal T PromoteHeader<T>(MarkingBinaryReader reader)
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

            return header;
        }

        protected virtual void PostReadingBytes(MarkingBinaryReader reader)
        {
        }

        /// <summary>
        /// This is the number of bytes read when reading the header
        /// </summary>
        protected long ReadBytes
        {
            get;
            private set;
        }

        protected short HeadCRC
        {
            get;
            private set;
        }

        internal HeaderType HeaderType
        {
            get;
            private set;
        }

        /// <summary>
        /// Untyped flags.  These should be typed when Promoting to another header
        /// </summary>
        protected short Flags
        {
            get;
            private set;
        }

        protected short HeaderSize
        {
            get;
            private set;
        }

        /// <summary>
        /// This additional size of the header could be file data
        /// </summary>
        protected uint AdditionalSize
        {
            get;
            private set;
        }
    }
}
