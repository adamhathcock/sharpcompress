using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Writers.Zip
{
    internal class ZipCentralDirectoryEntry
    {
        private readonly ZipCompressionMethod compression;
        private readonly string fileName;
        private readonly ArchiveEncoding archiveEncoding;

        public ZipCentralDirectoryEntry(ZipCompressionMethod compression, string fileName, ulong headerOffset, ArchiveEncoding archiveEncoding)
        {
            this.compression = compression;
            this.fileName = fileName;
            HeaderOffset = headerOffset;
            this.archiveEncoding = archiveEncoding;
        }

        internal DateTime? ModificationTime { get; set; }
        internal string? Comment { get; set; }
        internal uint Crc { get; set; }
        internal ulong Compressed { get; set; }
        internal ulong Decompressed { get; set; }
        internal ushort Zip64HeaderOffset { get; set; }
        internal ulong HeaderOffset { get; }

        internal uint Write(Stream outputStream)
        {
            byte[] encodedFilename = archiveEncoding.Encode(fileName);
            byte[] encodedComment = archiveEncoding.Encode(Comment ?? string.Empty);

            var zip64_stream = Compressed >= uint.MaxValue || Decompressed >= uint.MaxValue;
            var zip64 = zip64_stream || HeaderOffset >= uint.MaxValue;
            var usedCompression = compression;

            var compressedvalue = zip64 ? uint.MaxValue : (uint)Compressed;
            var decompressedvalue = zip64 ? uint.MaxValue : (uint)Decompressed;
            var headeroffsetvalue = zip64 ? uint.MaxValue : (uint)HeaderOffset;
            var extralength = zip64 ? (2 + 2 + 8 + 8 + 8 + 4) : 0;
            var version = (byte)(zip64 ? 45 : 20); // Version 20 required for deflate/encryption

            HeaderFlags flags = Equals(archiveEncoding.GetEncoding(), Encoding.UTF8) ? HeaderFlags.Efs : HeaderFlags.None;
            if (!outputStream.CanSeek)
            {
                // Cannot use data descriptors with zip64:
                // https://blogs.oracle.com/xuemingshen/entry/is_zipinput_outputstream_handling_of

                // We check that streams are not written too large in the ZipWritingStream,
                // so this extra guard is not required, but kept to simplify changing the code
                // once the zip64 post-data issue is resolved
                if (!zip64_stream)
                {
                    flags |= HeaderFlags.UsePostDataDescriptor;
                }

                if (usedCompression == ZipCompressionMethod.LZMA)
                {
                    flags |= HeaderFlags.Bit1; // eos marker
                }
            }

            // Support for zero byte files
            if (Decompressed == 0 && Compressed == 0)
            {
                usedCompression = ZipCompressionMethod.None;
            }

            Span<byte> intBuf = stackalloc byte[] { 80, 75, 1, 2, version, 0, version, 0 };
            //constant sig, then version made by, then version to extract
            outputStream.Write(intBuf);

            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
            outputStream.Write(intBuf.Slice(0, 2));
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)usedCompression);
            outputStream.Write(intBuf.Slice(0, 2)); // zipping method
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ModificationTime.DateTimeToDosTime());
            outputStream.Write(intBuf.Slice(0, 4));

            // zipping date and time
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, Crc);
            outputStream.Write(intBuf.Slice(0, 4)); // file CRC
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, compressedvalue);
            outputStream.Write(intBuf.Slice(0, 4)); // compressed file size
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, decompressedvalue);
            outputStream.Write(intBuf.Slice(0, 4)); // uncompressed file size
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedFilename.Length);
            outputStream.Write(intBuf.Slice(0, 2)); // Filename in zip
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
            outputStream.Write(intBuf.Slice(0, 2)); // extra length
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
            outputStream.Write(intBuf.Slice(0, 2));

            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0);
            outputStream.Write(intBuf.Slice(0, 2)); // disk=0
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
            outputStream.Write(intBuf.Slice(0, 2)); // file type: binary
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
            outputStream.Write(intBuf.Slice(0, 2)); // Internal file attributes
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x8100);
            outputStream.Write(intBuf.Slice(0, 2));

            // External file attributes (normal/readable)
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, headeroffsetvalue);
            outputStream.Write(intBuf.Slice(0, 4)); // Offset of header

            outputStream.Write(encodedFilename, 0, encodedFilename.Length);
            if (zip64)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                outputStream.Write(intBuf.Slice(0, 2));
                BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)(extralength - 4));
                outputStream.Write(intBuf.Slice(0, 2));

                BinaryPrimitives.WriteUInt64LittleEndian(intBuf, Decompressed);
                outputStream.Write(intBuf);
                BinaryPrimitives.WriteUInt64LittleEndian(intBuf, Compressed);
                outputStream.Write(intBuf);
                BinaryPrimitives.WriteUInt64LittleEndian(intBuf, HeaderOffset);
                outputStream.Write(intBuf);
                BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
                outputStream.Write(intBuf.Slice(0, 4)); // VolumeNumber = 0
            }

            outputStream.Write(encodedComment, 0, encodedComment.Length);

            return (uint)(8 + 2 + 2 + 4 + 4 + 4 + 4 + 2 + 2 + 2
                                    + 2 + 2 + 2 + 2 + 4 + encodedFilename.Length + extralength + encodedComment.Length);
        }
    }
}
