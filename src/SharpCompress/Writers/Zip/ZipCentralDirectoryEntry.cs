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

            byte[] intBuf = { 80, 75, 1, 2, version, 0, version, 0 };
            //constant sig, then version made by, then version to extract
            await outputStream.WriteAsync(intBuf, 0, 8, cancellationToken);

            await outputStream.WriteUInt16( (ushort)flags, cancellationToken);
            await outputStream.WriteUInt16( (ushort)usedCompression, cancellationToken);// zipping method
            await outputStream.WriteUInt32(ModificationTime.DateTimeToDosTime(), cancellationToken); // zipping date and time

            await outputStream.WriteUInt32(Crc, cancellationToken); // file CRC
            await outputStream.WriteUInt32(compressedvalue, cancellationToken); // compressed file size
            await outputStream.WriteUInt32(decompressedvalue, cancellationToken); // uncompressed file size
            await outputStream.WriteUInt16((ushort)encodedFilename.Length, cancellationToken); // Filename in zip
            await outputStream.WriteUInt16( (ushort)extralength, cancellationToken); // extra length
            await outputStream.WriteUInt16((ushort)encodedComment.Length, cancellationToken);

            await outputStream.WriteUInt16(0, cancellationToken); // disk=0
            await outputStream.WriteUInt16( (ushort)flags, cancellationToken); // file type: binary
            await outputStream.WriteUInt16( (ushort)flags, cancellationToken); // Internal file attributes
            await outputStream.WriteUInt16(0x8100, cancellationToken);

            // External file attributes (normal/readable)
            await outputStream.WriteUInt32(headeroffsetvalue, cancellationToken); // Offset of header

            await outputStream.WriteAsync(encodedFilename, 0, encodedFilename.Length, cancellationToken);
            if (zip64)
            {
                await outputStream.WriteUInt16(0x0001, cancellationToken);
                await outputStream.WriteUInt16((ushort)(extralength - 4), cancellationToken);

                await outputStream.WriteUInt64(Decompressed, cancellationToken);
                await outputStream.WriteUInt64(Compressed, cancellationToken);
                await outputStream.WriteUInt64(HeaderOffset, cancellationToken);
                await outputStream.WriteUInt32(0, cancellationToken); // VolumeNumber = 0
            }

            await outputStream.WriteAsync(encodedComment, 0, encodedComment.Length, cancellationToken);

            return (uint)(8 + 2 + 2 + 4 + 4 + 4 + 4 + 2 + 2 + 2
                                    + 2 + 2 + 2 + 2 + 4 + encodedFilename.Length + extralength + encodedComment.Length);
        }
    }
}
