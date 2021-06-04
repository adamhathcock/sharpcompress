#nullable disable

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress.Common.Zip.Headers
{
    internal abstract class ZipFileEntry : ZipHeader
    {
        protected ZipFileEntry(ZipHeaderType type, ArchiveEncoding archiveEncoding)
            : base(type)
        {
            Extra = new List<ExtraData>();
            ArchiveEncoding = archiveEncoding;
        }

        internal bool IsDirectory
        {
            get
            {
                if (Name.EndsWith('/'))
                {
                    return true;
                }

                //.NET Framework 4.5 : System.IO.Compression::CreateFromDirectory() probably writes backslashes to headers
                return CompressedSize == 0
                       && UncompressedSize == 0
                       && Name.EndsWith('\\');
            }
        }

        internal Stream PackedStream { get; set; }

        internal ArchiveEncoding ArchiveEncoding { get; }

        internal string Name { get; set; }

        internal HeaderFlags Flags { get; set; }

        internal ZipCompressionMethod CompressionMethod { get; set; }

        internal long CompressedSize { get; set; }

        internal long? DataStartPosition { get; set; }

        internal long UncompressedSize { get; set; }

        internal List<ExtraData> Extra { get; set; }

        public string Password { get; set; }

        internal PkwareTraditionalEncryptionData ComposeEncryptionData(Stream archiveStream)
        {
            if (archiveStream is null)
            {
                throw new ArgumentNullException(nameof(archiveStream));
            }

            var buffer = new byte[12];
            archiveStream.ReadFully(buffer);

            PkwareTraditionalEncryptionData encryptionData = PkwareTraditionalEncryptionData.ForRead(Password!, this, buffer);

            return encryptionData;
        }

        internal WinzipAesEncryptionData WinzipAesEncryptionData { get; set; }

        internal ushort LastModifiedDate { get; set; }

        internal ushort LastModifiedTime { get; set; }

        internal uint Crc { get; set; }

        protected void LoadExtra(byte[] extra)
        {
            for (int i = 0; i < extra.Length - 4;)
            {
                ExtraDataType type = (ExtraDataType)BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(i));
                if (!Enum.IsDefined(typeof(ExtraDataType), type))
                {
                    type = ExtraDataType.NotImplementedExtraData;
                }

                ushort length = BinaryPrimitives.ReadUInt16LittleEndian(extra.AsSpan(i + 2));

                // 7zip has this same kind of check to ignore extras blocks that don't conform to the standard 2-byte ID, 2-byte length, N-byte value.
                // CPP/7Zip/Zip/ZipIn.cpp: CInArchive::ReadExtra
                if (length > extra.Length)
                {
                    // bad extras block
                    return;
                }

                byte[] data = new byte[length];
                Buffer.BlockCopy(extra, i + 4, data, 0, length);
                Extra.Add(LocalEntryHeaderExtraFactory.Create(type, length, data));

                i += length + 4;
            }
        }

        internal ZipFilePart Part { get; set; }

        internal bool IsZip64 => CompressedSize >= uint.MaxValue;
    }
}
