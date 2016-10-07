using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SharpCompress.Converters;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip.Headers
{
    internal abstract class ZipFileEntry : ZipHeader
    {
        protected ZipFileEntry(ZipHeaderType type)
            : base(type)
        {
            Extra = new List<ExtraData>();
        }

        internal bool IsDirectory
        {
            get
            {
                if (Name.EndsWith("/"))
                {
                    return true;
                }

                //.NET Framework 4.5 : System.IO.Compression::CreateFromDirectory() probably writes backslashes to headers
                return CompressedSize == 0
                       && UncompressedSize == 0
                       && Name.EndsWith("\\");
            }
        }

        protected string DecodeString(ByteArrayPoolScope str)
        {
            if (FlagUtility.HasFlag(Flags, HeaderFlags.UTF8))
            {
                return Encoding.UTF8.GetString(str.Array, 0, str.Count);
            }

            return ArchiveEncoding.Default.GetString(str.Array, 0, str.Count);
        }

        protected byte[] EncodeString(string str)
        {
            if (FlagUtility.HasFlag(Flags, HeaderFlags.UTF8))
            {
                return Encoding.UTF8.GetBytes(str);
            }
            return ArchiveEncoding.Default.GetBytes(str);
        }

        internal Stream PackedStream { get; set; }

        internal string Name { get; set; }

        internal HeaderFlags Flags { get; set; }

        internal ZipCompressionMethod CompressionMethod { get; set; }

        internal uint CompressedSize { get; set; }

        internal long? DataStartPosition { get; set; }

        internal uint UncompressedSize { get; set; }

        internal List<ExtraData> Extra { get; set; }

        internal PkwareTraditionalEncryptionData PkwareTraditionalEncryptionData { get; set; }
#if !NO_CRYPTO
        internal WinzipAesEncryptionData WinzipAesEncryptionData { get; set; }
#endif

        internal ushort LastModifiedDate { get; set; }

        internal ushort LastModifiedTime { get; set; }

        internal uint Crc { get; set; }

        protected void LoadExtra(ByteArrayPoolScope extra)
        {
            for (int i = 0; i < extra.Count - 4;)
            {
                ExtraDataType type = (ExtraDataType)DataConverter.LittleEndian.GetUInt16(extra.Array, i);
                if (!Enum.IsDefined(typeof(ExtraDataType), type))
                {
                    type = ExtraDataType.NotImplementedExtraData;
                }

                ushort length = DataConverter.LittleEndian.GetUInt16(extra.Array, i + 2);
                using (var data = ByteArrayPool.RentScope(length))
                {
                    Buffer.BlockCopy(extra.Array, i + 4, data.Array, 0, length);
                    Extra.Add(LocalEntryHeaderExtraFactory.Create(type, length, data.Array));
                }

                i += length + 4;
            }
        }

        internal ZipFilePart Part { get; set; }
    }
}