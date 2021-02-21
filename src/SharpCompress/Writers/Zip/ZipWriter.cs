using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
//using SharpCompress.Compressors.PPMd;
using SharpCompress.IO;

namespace SharpCompress.Writers.Zip
{
    public class ZipWriter : AbstractWriter
    {
        private readonly CompressionType compressionType;
        private readonly CompressionLevel compressionLevel;
        private readonly List<ZipCentralDirectoryEntry> entries = new();
        private readonly string zipComment;
        private long streamPosition;
       // private PpmdProperties? ppmdProps;
        private readonly bool isZip64;

        public ZipWriter(Stream destination, ZipWriterOptions zipWriterOptions)
            : base(ArchiveType.Zip, zipWriterOptions)
        {
            zipComment = zipWriterOptions.ArchiveComment ?? string.Empty;
            isZip64 = zipWriterOptions.UseZip64;
            if (destination.CanSeek)
            {
                streamPosition = destination.Position;
            }

            compressionType = zipWriterOptions.CompressionType;
            compressionLevel = zipWriterOptions.DeflateCompressionLevel;

            if (WriterOptions.LeaveStreamOpen)
            {
                destination = new NonDisposingStream(destination);
            }
            InitializeStream(destination);
        }

       /* private PpmdProperties PpmdProperties
        {
            get
            {
                return ppmdProps ??= new PpmdProperties();
            }
        }          */

        protected override async ValueTask DisposeAsyncCore()
        {
            ulong size = 0;
            foreach (ZipCentralDirectoryEntry entry in entries)
            {
                size += await entry.WriteAsync(OutputStream, CancellationToken.None);
            }
            await WriteEndRecordAsync(size);
            await base.DisposeAsyncCore();
        }

        private static ZipCompressionMethod ToZipCompressionMethod(CompressionType compressionType)
        {
            switch (compressionType)
            {
                case CompressionType.None:
                    {
                        return ZipCompressionMethod.None;
                    }
                case CompressionType.Deflate:
                    {
                        return ZipCompressionMethod.Deflate;
                    }
                case CompressionType.BZip2:
                    {
                        return ZipCompressionMethod.BZip2;
                    }
                case CompressionType.LZMA:
                    {
                        return ZipCompressionMethod.LZMA;
                    }
                case CompressionType.PPMd:
                    {
                        return ZipCompressionMethod.PPMd;
                    }
                default:
                    throw new InvalidFormatException("Invalid compression method: " + compressionType);
            }
        }

        public override ValueTask WriteAsync(string filename, Stream source, DateTime? modificationTime, CancellationToken cancellationToken = default)
        {
            return WriteAsync(filename, source, new ZipWriterEntryOptions()
            {
                ModificationDateTime = modificationTime
                                         }, cancellationToken);
        }

        public async ValueTask WriteAsync(string entryPath, Stream source, ZipWriterEntryOptions zipWriterEntryOptions, CancellationToken cancellationToken = default)
        {
            await using Stream output = await WriteToStreamAsync(entryPath, zipWriterEntryOptions, cancellationToken);
            await source.CopyToAsync(output, cancellationToken);
        }

        public async ValueTask<Stream> WriteToStreamAsync(string entryPath, ZipWriterEntryOptions options, CancellationToken cancellationToken = default)
        {
            var compression = ToZipCompressionMethod(options.CompressionType ?? compressionType);

            entryPath = NormalizeFilename(entryPath);
            options.ModificationDateTime ??= DateTime.Now;
            options.EntryComment ??= string.Empty;
            var entry = new ZipCentralDirectoryEntry(compression, entryPath, (ulong)streamPosition, WriterOptions.ArchiveEncoding)
            {
                Comment = options.EntryComment,
                ModificationTime = options.ModificationDateTime
            };

            // Use the archive default setting for zip64 and allow overrides
            var useZip64 = isZip64;
            if (options.EnableZip64.HasValue)
            {
                useZip64 = options.EnableZip64.Value;
            }

            var headersize = (uint)(await WriteHeaderAsync(entryPath, options, entry, useZip64, cancellationToken));
            streamPosition += headersize;
            var s = new ZipWritingStream(this, OutputStream, entry, compression,
                options.DeflateCompressionLevel ?? compressionLevel);
            await s.InitializeAsync(cancellationToken);
            return s;
        }

        private string NormalizeFilename(string filename)
        {
            filename = filename.Replace('\\', '/');

            int pos = filename.IndexOf(':');
            if (pos >= 0)
            {
                filename = filename.Remove(0, pos + 1);
            }

            return filename.Trim('/');
        }

        private async ValueTask<int> WriteHeaderAsync(string filename, ZipWriterEntryOptions zipWriterEntryOptions, ZipCentralDirectoryEntry entry, bool useZip64, 
                                                      CancellationToken cancellationToken)
        {
            // We err on the side of caution until the zip specification clarifies how to support this
            if (!OutputStream.CanSeek && useZip64)
            {
                throw new NotSupportedException("Zip64 extensions are not supported on non-seekable streams");
            }

            var explicitZipCompressionInfo = ToZipCompressionMethod(zipWriterEntryOptions.CompressionType ?? compressionType);
            byte[] encodedFilename = WriterOptions.ArchiveEncoding.Encode(filename);

            await OutputStream.WriteUInt32(ZipHeaderFactory.ENTRY_HEADER_BYTES, cancellationToken: cancellationToken);
            if (explicitZipCompressionInfo == ZipCompressionMethod.Deflate)
            {
                if (OutputStream.CanSeek && useZip64)
                {
                    await OutputStream.WriteBytes(45, 0 ); //smallest allowed version for zip64
                }
                else
                {
                    await OutputStream.WriteBytes(20, 0 ); //older version which is more compatible
                }
            }
            else
            {
                await OutputStream.WriteBytes(63, 0); //version says we used PPMd or LZMA
            }
            HeaderFlags flags = Equals(WriterOptions.ArchiveEncoding.GetEncoding(), Encoding.UTF8) ? HeaderFlags.Efs : 0;
            if (!OutputStream.CanSeek)
            {
                flags |= HeaderFlags.UsePostDataDescriptor;

                if (explicitZipCompressionInfo == ZipCompressionMethod.LZMA)
                {
                    flags |= HeaderFlags.Bit1; // eos marker
                }
            }

            await OutputStream.WriteUInt16((ushort)flags, cancellationToken: cancellationToken);
            await OutputStream.WriteUInt16((ushort)explicitZipCompressionInfo, cancellationToken: cancellationToken); // zipping method
            await OutputStream.WriteUInt32(zipWriterEntryOptions.ModificationDateTime.DateTimeToDosTime(), cancellationToken: cancellationToken); // zipping date and time
            // unused CRC, un/compressed size, updated later
            await OutputStream.WriteBytes(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            await OutputStream.WriteUInt16((ushort)encodedFilename.Length, cancellationToken: cancellationToken);// filename length

            var extralength = 0;
            if (OutputStream.CanSeek && useZip64)
            {
                extralength = 2 + 2 + 8 + 8;
            }

            await OutputStream.WriteUInt16( (ushort)extralength, cancellationToken: cancellationToken); // extra length
            await OutputStream.WriteAsync(encodedFilename, 0, encodedFilename.Length, cancellationToken);

            if (extralength != 0)
            {
                await OutputStream.WriteAsync(new byte[extralength], 0, extralength, cancellationToken); // reserve space for zip64 data
                entry.Zip64HeaderOffset = (ushort)(6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length);
            }

            return 6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length + extralength;
        }

        private async ValueTask WriteFooterAsync(uint crc, uint compressed, uint uncompressed)
        {
            await OutputStream.WriteUInt32(crc);
            await OutputStream.WriteUInt32(compressed);
            await OutputStream.WriteUInt32(uncompressed);
        }

        private async ValueTask WriteEndRecordAsync(ulong size)
        {

            var zip64 = isZip64 || entries.Count > ushort.MaxValue || streamPosition >= uint.MaxValue || size >= uint.MaxValue;

            var sizevalue = size >= uint.MaxValue ? uint.MaxValue : (uint)size;
            var streampositionvalue = streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)streamPosition;

            if (zip64)
            {
                var recordlen = 2 + 2 + 4 + 4 + 8 + 8 + 8 + 8;

                // Write zip64 end of central directory record
                await OutputStream.WriteBytes(80, 75, 6, 6);

                await OutputStream.WriteUInt64((ulong)recordlen); // Size of zip64 end of central directory record
                await OutputStream.WriteUInt16(45); // Made by
                await OutputStream.WriteUInt16(45); // Version needed

                await OutputStream.WriteUInt32(0); // Disk number
                await OutputStream.WriteUInt32(0); // Central dir disk

                // TODO: entries.Count is int, so max 2^31 files
                await OutputStream.WriteUInt64((ulong)entries.Count); // Entries in this disk
                await OutputStream.WriteUInt64((ulong)entries.Count); // Total entries
                await OutputStream.WriteUInt64(size);  // Central Directory size
                await OutputStream.WriteUInt64((ulong)streamPosition); // Disk offset

                // Write zip64 end of central directory locator
                await OutputStream.WriteBytes( 80, 75, 6, 7);

                await OutputStream.WriteUInt32(0);// Entry disk
                await OutputStream.WriteUInt64((ulong)streamPosition + size); // Offset to the zip64 central directory
                await OutputStream.WriteUInt32( 1); // Number of disks

                streamPosition += recordlen + (4 + 4 + 8 + 4);
                streampositionvalue = streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)streampositionvalue;
            }

            // Write normal end of central directory record
            await OutputStream.WriteBytes(80, 75, 5, 6, 0, 0, 0, 0);
            await OutputStream.WriteUInt16((ushort)entries.Count);
            await OutputStream.WriteUInt16((ushort)entries.Count);
            await OutputStream.WriteUInt32(sizevalue);
            await OutputStream.WriteUInt32( streampositionvalue);
            byte[] encodedComment = WriterOptions.ArchiveEncoding.Encode(zipComment);
            await OutputStream.WriteUInt16((ushort)encodedComment.Length);
            await OutputStream.WriteAsync(encodedComment, 0, encodedComment.Length);
        }

        #region Nested type: ZipWritingStream

        private class ZipWritingStream : AsyncStream
        {
            private readonly CRC32 crc = new CRC32();
            private readonly ZipCentralDirectoryEntry entry;
            private readonly Stream originalStream;
#nullable disable
            private Stream writeStream;
#nullable enable
            private readonly ZipWriter writer;
            private readonly ZipCompressionMethod zipCompressionMethod;
            private readonly CompressionLevel compressionLevel;
            private CountingWritableSubStream? counting;
            private ulong decompressed;

            // Flag to prevent throwing exceptions on Dispose
            private bool limitsExceeded;
            private bool isDisposed;

            internal ZipWritingStream(ZipWriter writer, Stream originalStream, ZipCentralDirectoryEntry entry,
                ZipCompressionMethod zipCompressionMethod, CompressionLevel compressionLevel)
            {
                this.writer = writer;
                this.originalStream = originalStream;
                this.writer = writer;
                this.entry = entry;
                this.zipCompressionMethod = zipCompressionMethod;
                this.compressionLevel = compressionLevel;
            }

            public async ValueTask InitializeAsync(CancellationToken cancellationToken)
            {
                writeStream = await GetWriteStream(originalStream, cancellationToken);
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            private async ValueTask<Stream> GetWriteStream(Stream writeStream, CancellationToken cancellationToken)
            {
                counting = new CountingWritableSubStream(writeStream);
                Stream output = counting;
                switch (zipCompressionMethod)
                {
                    case ZipCompressionMethod.None:
                        {
                            return output;
                        }
                    case ZipCompressionMethod.Deflate:
                        {
                            return new DeflateStream(counting, CompressionMode.Compress, compressionLevel);
                        }
                    /*case ZipCompressionMethod.BZip2:
                        {
                            return await BZip2Stream.CreateAsync(counting, CompressionMode.Compress, false, cancellationToken);
                        } */
                    case ZipCompressionMethod.LZMA:
                        {
                            await counting.WriteBytes(9, 20, 5, 0);

                            LzmaStream lzmaStream = new LzmaStream(new LzmaEncoderProperties(!originalStream.CanSeek),
                                                                   false, counting);
                            await counting.WriteAsync(lzmaStream.Properties, 0, lzmaStream.Properties.Length, cancellationToken);
                            return lzmaStream;
                        }
                  /*  case ZipCompressionMethod.PPMd:
                        {
                            counting.Write(writer.PpmdProperties.Properties, 0, 2);
                            return new PpmdStream(writer.PpmdProperties, counting, true);
                        }                         */
                    default:
                        {
                            throw new NotSupportedException("CompressionMethod: " + zipCompressionMethod);
                        }
                }
            }

            public override async ValueTask DisposeAsync()
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;

                await writeStream.DisposeAsync();

                    if (limitsExceeded)
                    {
                        // We have written invalid data into the archive,
                        // so we destroy it now, instead of allowing the user to continue
                        // with a defunct archive
                    await originalStream.DisposeAsync();
                        return;
                    }

                    entry.Crc = (uint)crc.Crc32Result;
                    entry.Compressed = counting!.Count;
                    entry.Decompressed = decompressed;

                    var zip64 = entry.Compressed >= uint.MaxValue || entry.Decompressed >= uint.MaxValue;
                    var compressedvalue = zip64 ? uint.MaxValue : (uint)counting.Count;
                    var decompressedvalue = zip64 ? uint.MaxValue : (uint)entry.Decompressed;

                    if (originalStream.CanSeek)
                    {
                        originalStream.Position = (long)(entry.HeaderOffset + 6);
                        await originalStream.WriteByteAsync(0);

                        if (counting.Count == 0 && entry.Decompressed == 0)
                        {
                            // set compression to STORED for zero byte files (no compression data)
                            originalStream.Position = (long)(entry.HeaderOffset + 8);
                            await  originalStream.WriteByteAsync(0);
                            await originalStream.WriteByteAsync(0);
                        }

                        originalStream.Position = (long)(entry.HeaderOffset + 14);

                        await writer.WriteFooterAsync(entry.Crc, compressedvalue, decompressedvalue);

                        // Ideally, we should not throw from Dispose()
                        // We should not get here as the Write call checks the limits
                        if (zip64 && entry.Zip64HeaderOffset == 0)
                        {
                            throw new NotSupportedException("Attempted to write a stream that is larger than 4GiB without setting the zip64 option");
                        }

                        // If we have pre-allocated space for zip64 data,
                        // fill it out, even if it is not required
                        if (entry.Zip64HeaderOffset != 0)
                        {
                            originalStream.Position = (long)(entry.HeaderOffset + entry.Zip64HeaderOffset);
                            await originalStream.WriteUInt16(0x0001);
                            await originalStream.WriteUInt16(8 + 8);

                            await originalStream.WriteUInt64(entry.Decompressed);
                            await originalStream.WriteUInt64(entry.Compressed);
                        }

                        originalStream.Position = writer.streamPosition + (long)entry.Compressed;
                        writer.streamPosition += (long)entry.Compressed;
                    }
                    else
                    {
                        // We have a streaming archive, so we should add a post-data-descriptor,
                        // but we cannot as it does not hold the zip64 values
                        // Throwing an exception until the zip specification is clarified

                        // Ideally, we should not throw from Dispose()
                        // We should not get here as the Write call checks the limits
                        if (zip64)
                        {
                            throw new NotSupportedException("Streams larger than 4GiB are not supported for non-seekable streams");
                        }

                        await originalStream.WriteUInt32(ZipHeaderFactory.POST_DATA_DESCRIPTOR);
                        await writer.WriteFooterAsync(entry.Crc,
                                           compressedvalue,
                                           decompressedvalue);
                        writer.streamPosition += (long)entry.Compressed + 16;
                    }
                    writer.entries.Add(entry);
                }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return writeStream.FlushAsync(cancellationToken);
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                // We check the limits first, because we can keep the archive consistent
                // if we can prevent the writes from happening
                if (entry.Zip64HeaderOffset == 0)
                {
                    // Pre-check, the counting.Count is not exact, as we do not know the size before having actually compressed it
                    if (limitsExceeded || ((decompressed + (uint)count) > uint.MaxValue) || (counting!.Count + (uint)count) > uint.MaxValue)
                    {
                        throw new NotSupportedException("Attempted to write a stream that is larger than 4GiB without setting the zip64 option");
                    }
                }

                decompressed += (uint)count;
                crc.SlurpBlock(buffer, offset, count);
                await writeStream.WriteAsync(buffer, offset, count, cancellationToken);

                if (entry.Zip64HeaderOffset == 0)
                {
                    // Post-check, this is accurate
                    if ((decompressed > uint.MaxValue) || counting!.Count > uint.MaxValue)
                    {
                        // We have written the data, so the archive is now broken
                        // Throwing the exception here, allows us to avoid
                        // throwing an exception in Dispose() which is discouraged
                        // as it can mask other errors
                        limitsExceeded = true;
                        throw new NotSupportedException("Attempted to write a stream that is larger than 4GiB without setting the zip64 option");
                    }
                }
            }
        }

        #endregion Nested type: ZipWritingStream
    }
}
