namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.IO;
    using System;
    using System.IO;

    internal class ZipHeaderFactory
    {
        internal const uint DIGITAL_SIGNATURE = 0x5054b50;
        internal const uint DIRECTORY_END_HEADER_BYTES = 0x6054b50;
        internal const uint DIRECTORY_START_HEADER_BYTES = 0x2014b50;
        internal const uint ENTRY_HEADER_BYTES = 0x4034b50;
        protected LocalEntryHeader lastEntryHeader;
        private StreamingMode mode;
        private string password;
        internal const uint POST_DATA_DESCRIPTOR = 0x8074b50;
        internal const uint SPLIT_ARCHIVE_HEADER_BYTES = 0x30304b50;
        private const uint ZIP64_END_OF_CENTRAL_DIRECTORY = 0x6064b50;
        private const uint ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR = 0x7064b50;

        protected ZipHeaderFactory(StreamingMode mode, string password)
        {
            this.mode = mode;
            this.password = password;
        }

        internal static bool IsHeader(uint headerBytes)
        {
            switch (headerBytes)
            {
                case 0x5054b50:
                case 0x6054b50:
                case 0x2014b50:
                case 0x4034b50:
                case 0x8074b50:
                case 0x30304b50:
                case 0x6064b50:
                case 0x7064b50:
                    return true;
            }
            return false;
        }

        private void LoadHeader(ZipFileEntry entryHeader, Stream stream)
        {
            if (FlagUtility.HasFlag<HeaderFlags>(entryHeader.Flags, HeaderFlags.Encrypted))
            {
                if ((!entryHeader.IsDirectory && (entryHeader.CompressedSize == 0)) && FlagUtility.HasFlag<HeaderFlags>(entryHeader.Flags, HeaderFlags.UsePostDataDescriptor))
                {
                    throw new NotSupportedException("SharpCompress cannot currently read non-seekable Zip Streams with encrypted data that has been written in a non-seekable manner.");
                }
                if (this.password == null)
                {
                    throw new CryptographicException("No password supplied for encrypted zip.");
                }
                if (entryHeader.CompressionMethod == ZipCompressionMethod.WinzipAes)
                {
                    throw new NotSupportedException("Cannot decrypt Winzip AES with Silverlight or WP7.");
                }
                byte[] buffer = new byte[12];
                stream.Read(buffer, 0, 12);
                entryHeader.PkwareTraditionalEncryptionData = PkwareTraditionalEncryptionData.ForRead(this.password, entryHeader, buffer);
                entryHeader.CompressedSize -= 12;
            }
            if (!entryHeader.IsDirectory)
            {
                switch (this.mode)
                {
                    case StreamingMode.Streaming:
                        entryHeader.PackedStream = stream;
                        return;

                    case StreamingMode.Seekable:
                        entryHeader.DataStartPosition = new long?(stream.Position);
                        stream.Position += entryHeader.CompressedSize;
                        return;
                }
                throw new InvalidFormatException("Invalid StreamingMode");
            }
        }

        protected ZipHeader ReadHeader(uint headerBytes, BinaryReader reader)
        {
            switch (headerBytes)
            {
                case 0x5054b50:
                    return null;

                case 0x6054b50:
                {
                    DirectoryEndHeader header3 = new DirectoryEndHeader();
                    header3.Read(reader);
                    return header3;
                }
                case 0x2014b50:
                {
                    DirectoryEntryHeader header2 = new DirectoryEntryHeader();
                    header2.Read(reader);
                    return header2;
                }
                case 0x4034b50:
                {
                    LocalEntryHeader entryHeader = new LocalEntryHeader();
                    entryHeader.Read(reader);
                    this.LoadHeader(entryHeader, reader.BaseStream);
                    this.lastEntryHeader = entryHeader;
                    return entryHeader;
                }
                case 0x6064b50:
                case 0x7064b50:
                {
                    IgnoreHeader header4 = new IgnoreHeader(ZipHeaderType.Ignore);
                    header4.Read(reader);
                    return header4;
                }
                case 0x8074b50:
                    if (FlagUtility.HasFlag<HeaderFlags>(this.lastEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor))
                    {
                        this.lastEntryHeader.Crc = reader.ReadUInt32();
                        this.lastEntryHeader.CompressedSize = reader.ReadUInt32();
                        this.lastEntryHeader.UncompressedSize = reader.ReadUInt32();
                    }
                    else
                    {
                        reader.ReadUInt32();
                        reader.ReadUInt32();
                        reader.ReadUInt32();
                    }
                    return null;

                case 0x30304b50:
                    return new SplitHeader();
            }
            throw new NotSupportedException("Unknown header: " + headerBytes);
        }
    }
}

