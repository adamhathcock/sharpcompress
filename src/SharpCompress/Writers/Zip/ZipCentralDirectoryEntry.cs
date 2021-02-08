using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

        internal async ValueTask<uint> WriteAsync(Stream outputStream, CancellationToken cancellationToken)
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

            byte[] intBuf = new byte[] { 80, 75, 1, 2, version, 0, version, 0 };
            //constant sig, then version made by, then version to extract
            await outputStream.WriteAsync(intBuf, 0, 8, cancellationToken);

            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken);
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)usedCompression);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken); // zipping method
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ModificationTime.DateTimeToDosTime());
            await outputStream.WriteAsync(intBuf, 0, 4, cancellationToken);

            // zipping date and time
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, Crc);
            await outputStream.WriteAsync(intBuf, 0, 4, cancellationToken); // file CRC
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, compressedvalue);
            await outputStream.WriteAsync(intBuf, 0, 4, cancellationToken); // compressed file size
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, decompressedvalue);
            await outputStream.WriteAsync(intBuf, 0, 4, cancellationToken); // uncompressed file size
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedFilename.Length);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken); // Filename in zip
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken); // extra length
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken);

            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken); // disk=0
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken); // file type: binary
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken); // Internal file attributes
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x8100);
            await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken);

            // External file attributes (normal/readable)
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, headeroffsetvalue);
            await outputStream.WriteAsync(intBuf, 0, 4, cancellationToken); // Offset of header

            await outputStream.WriteAsync(encodedFilename, 0, encodedFilename.Length, cancellationToken);
            if (zip64)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken);
                BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)(extralength - 4));
                await outputStream.WriteAsync(intBuf, 0, 2, cancellationToken);

                BinaryPrimitives.WriteUInt64LittleEndian(intBuf, Decompressed);
                await outputStream.WriteAsync(intBuf, 0, 8, cancellationToken);
                BinaryPrimitives.WriteUInt64LittleEndian(intBuf, Compressed);
                await outputStream.WriteAsync(intBuf, 0, 8, cancellationToken);
                BinaryPrimitives.WriteUInt64LittleEndian(intBuf, HeaderOffset);
                await outputStream.WriteAsync(intBuf, 0, 8, cancellationToken);
                BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
                await outputStream.WriteAsync(intBuf, 0, 4, cancellationToken); // VolumeNumber = 0
            }

            await outputStream.WriteAsync(encodedComment, 0, encodedComment.Length, cancellationToken);

            return (uint)(8 + 2 + 2 + 4 + 4 + 4 + 4 + 2 + 2 + 2
                                    + 2 + 2 + 2 + 2 + 4 + encodedFilename.Length + extralength + encodedComment.Length);
        }
    }
}
