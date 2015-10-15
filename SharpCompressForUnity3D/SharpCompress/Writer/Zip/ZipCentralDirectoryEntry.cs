namespace SharpCompress.Writer.Zip
{
    using SharpCompress;
    using SharpCompress.Common.Zip;
    using SharpCompress.Common.Zip.Headers;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;

    internal class ZipCentralDirectoryEntry
    {
        [CompilerGenerated]
        private string <Comment>k__BackingField;
        [CompilerGenerated]
        private uint <Compressed>k__BackingField;
        [CompilerGenerated]
        private uint <Crc>k__BackingField;
        [CompilerGenerated]
        private uint <Decompressed>k__BackingField;
        [CompilerGenerated]
        private string <FileName>k__BackingField;
        [CompilerGenerated]
        private uint <HeaderOffset>k__BackingField;
        [CompilerGenerated]
        private DateTime? <ModificationTime>k__BackingField;

        internal uint Write(Stream outputStream, ZipCompressionMethod compression)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(this.FileName);
            byte[] buffer = Encoding.UTF8.GetBytes(this.Comment);
            outputStream.Write(new byte[] { 80, 0x4b, 1, 2, 0x3f, 0, 10, 0 }, 0, 8);
            HeaderFlags flags = HeaderFlags.UTF8;
            if (!outputStream.CanSeek)
            {
                flags = (HeaderFlags) ((ushort) (flags | HeaderFlags.UsePostDataDescriptor));
                if (compression == ZipCompressionMethod.LZMA)
                {
                    flags = (HeaderFlags) ((ushort) (flags | HeaderFlags.Bit1));
                }
            }
            outputStream.Write(BitConverter.GetBytes((ushort) flags), 0, 2);
            outputStream.Write(BitConverter.GetBytes((ushort) compression), 0, 2);
            outputStream.Write(BitConverter.GetBytes(Utility.DateTimeToDosTime(this.ModificationTime)), 0, 4);
            outputStream.Write(BitConverter.GetBytes(this.Crc), 0, 4);
            outputStream.Write(BitConverter.GetBytes(this.Compressed), 0, 4);
            outputStream.Write(BitConverter.GetBytes(this.Decompressed), 0, 4);
            outputStream.Write(BitConverter.GetBytes((ushort) bytes.Length), 0, 2);
            outputStream.Write(BitConverter.GetBytes((ushort) 0), 0, 2);
            outputStream.Write(BitConverter.GetBytes((ushort) buffer.Length), 0, 2);
            outputStream.Write(BitConverter.GetBytes((ushort) 0), 0, 2);
            outputStream.Write(BitConverter.GetBytes((ushort) 0), 0, 2);
            outputStream.Write(BitConverter.GetBytes((ushort) 0), 0, 2);
            outputStream.Write(BitConverter.GetBytes((ushort) 0x8100), 0, 2);
            outputStream.Write(BitConverter.GetBytes(this.HeaderOffset), 0, 4);
            outputStream.Write(bytes, 0, bytes.Length);
            outputStream.Write(buffer, 0, buffer.Length);
            return (uint) ((0x2e + bytes.Length) + buffer.Length);
        }

        internal string Comment
        {
            [CompilerGenerated]
            get
            {
                return this.<Comment>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Comment>k__BackingField = value;
            }
        }

        internal uint Compressed
        {
            [CompilerGenerated]
            get
            {
                return this.<Compressed>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Compressed>k__BackingField = value;
            }
        }

        internal uint Crc
        {
            [CompilerGenerated]
            get
            {
                return this.<Crc>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Crc>k__BackingField = value;
            }
        }

        internal uint Decompressed
        {
            [CompilerGenerated]
            get
            {
                return this.<Decompressed>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Decompressed>k__BackingField = value;
            }
        }

        internal string FileName
        {
            [CompilerGenerated]
            get
            {
                return this.<FileName>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<FileName>k__BackingField = value;
            }
        }

        internal uint HeaderOffset
        {
            [CompilerGenerated]
            get
            {
                return this.<HeaderOffset>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<HeaderOffset>k__BackingField = value;
            }
        }

        internal DateTime? ModificationTime
        {
            [CompilerGenerated]
            get
            {
                return this.<ModificationTime>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<ModificationTime>k__BackingField = value;
            }
        }
    }
}

