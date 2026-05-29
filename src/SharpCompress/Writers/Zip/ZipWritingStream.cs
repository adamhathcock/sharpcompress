using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Writers.Zip;

public partial class ZipWriter
{
    internal class ZipWritingStream : Stream
    {
        private readonly CRC32 crc = new();
        private readonly ZipCentralDirectoryEntry entry;
        private readonly Stream originalStream;
        private Stream writeStream;
        private readonly ZipWriter writer;
        private readonly ZipCompressionMethod zipCompressionMethod;
        private readonly int compressionLevel;
        private ICompressionProviderHooks? compressionProviderHooks;
        private CompressionContext? compressionContext;
        private CountingStream? counting;
        private ulong decompressed;

        // Flag to prevent throwing exceptions on Dispose
        private bool limitsExceeded;
        private bool isDisposed;

        internal ZipWritingStream(
            ZipWriter writer,
            Stream originalStream,
            ZipCentralDirectoryEntry entry,
            ZipCompressionMethod zipCompressionMethod,
            int compressionLevel,
            Stream? compressionStream = null
        )
            : this(writer, originalStream, entry, zipCompressionMethod, compressionLevel)
        {
            writeStream = GetWriteStream(compressionStream ?? originalStream);
        }

        private ZipWritingStream(
            ZipWriter writer,
            Stream originalStream,
            ZipCentralDirectoryEntry entry,
            ZipCompressionMethod zipCompressionMethod,
            int compressionLevel
        )
        {
            this.writer = writer;
            this.originalStream = originalStream;
            this.entry = entry;
            this.zipCompressionMethod = zipCompressionMethod;
            this.compressionLevel = compressionLevel;
            writeStream = Stream.Null;
        }

        internal static async ValueTask<ZipWritingStream> CreateAsync(
            ZipWriter writer,
            Stream originalStream,
            ZipCentralDirectoryEntry entry,
            ZipCompressionMethod zipCompressionMethod,
            int compressionLevel,
            CancellationToken cancellationToken
        )
        {
            var stream = new ZipWritingStream(
                writer,
                originalStream,
                entry,
                zipCompressionMethod,
                compressionLevel
            );
            stream.writeStream = await stream
                .GetWriteStreamAsync(originalStream, cancellationToken)
                .ConfigureAwait(false);
            return stream;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        private Stream GetWriteStream(Stream writeStream)
        {
            counting = new CountingStream(SharpCompressStream.CreateNonDisposing(writeStream));
            Stream output = counting;

            var providers = writer.WriterOptions.Providers;

            switch (zipCompressionMethod)
            {
                case ZipCompressionMethod.None:
                {
                    return output;
                }
                case ZipCompressionMethod.Deflate:
                {
                    return providers.CreateCompressStream(
                        CompressionType.Deflate,
                        counting,
                        compressionLevel
                    );
                }
                case ZipCompressionMethod.BZip2:
                {
                    return providers.CreateCompressStream(
                        CompressionType.BZip2,
                        counting,
                        compressionLevel
                    );
                }
                case ZipCompressionMethod.LZMA:
                {
                    var compressingProvider = providers.GetCompressingProvider(
                        CompressionType.LZMA
                    );
                    if (compressingProvider is null)
                    {
                        throw new ArchiveOperationException("LZMA compression provider not found.");
                    }

                    var context = new CompressionContext { CanSeek = originalStream.CanSeek };
                    compressionProviderHooks = compressingProvider;
                    compressionContext = context;

                    var preData = compressingProvider.GetPreCompressionData(context);
                    if (preData is not null)
                    {
                        counting.Write(preData, 0, preData.Length);
                    }

                    var lzmaStream = compressingProvider.CreateCompressStream(
                        counting,
                        compressionLevel,
                        context
                    );

                    var props = compressingProvider.GetCompressionProperties(lzmaStream, context);
                    if (props is not null)
                    {
                        counting.Write(props, 0, props.Length);
                    }

                    return lzmaStream;
                }
                case ZipCompressionMethod.PPMd:
                {
                    var compressingProvider = providers.GetCompressingProvider(
                        CompressionType.PPMd
                    );
                    if (compressingProvider is null)
                    {
                        throw new ArchiveOperationException("PPMd compression provider not found.");
                    }

                    var context = new CompressionContext
                    {
                        CanSeek = originalStream.CanSeek,
                        FormatOptions = writer.PpmdProperties,
                    };
                    compressionProviderHooks = compressingProvider;
                    compressionContext = context;

                    var preData = compressingProvider.GetPreCompressionData(context);
                    if (preData is not null)
                    {
                        counting.Write(preData, 0, preData.Length);
                    }

                    return compressingProvider.CreateCompressStream(
                        counting,
                        compressionLevel,
                        context
                    );
                }
                case ZipCompressionMethod.ZStandard:
                {
                    return providers.CreateCompressStream(
                        CompressionType.ZStandard,
                        counting,
                        compressionLevel
                    );
                }
                default:
                {
                    throw new NotSupportedException("CompressionMethod: " + zipCompressionMethod);
                }
            }
        }

        private async ValueTask<Stream> GetWriteStreamAsync(
            Stream writeStream,
            CancellationToken cancellationToken
        )
        {
            counting = new CountingStream(SharpCompressStream.CreateNonDisposing(writeStream));
            Stream output = counting;

            var providers = writer.WriterOptions.Providers;

            switch (zipCompressionMethod)
            {
                case ZipCompressionMethod.None:
                {
                    return output;
                }
                case ZipCompressionMethod.Deflate:
                {
                    return await providers
                        .CreateCompressStreamAsync(
                            CompressionType.Deflate,
                            counting,
                            compressionLevel,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                case ZipCompressionMethod.BZip2:
                {
                    return await providers
                        .CreateCompressStreamAsync(
                            CompressionType.BZip2,
                            counting,
                            compressionLevel,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                case ZipCompressionMethod.LZMA:
                {
                    var compressingProvider = providers.GetCompressingProvider(
                        CompressionType.LZMA
                    );
                    if (compressingProvider is null)
                    {
                        throw new ArchiveOperationException("LZMA compression provider not found.");
                    }

                    var context = new CompressionContext { CanSeek = originalStream.CanSeek };
                    compressionProviderHooks = compressingProvider;
                    compressionContext = context;

                    var preData = compressingProvider.GetPreCompressionData(context);
                    if (preData is not null)
                    {
                        await counting
                            .WriteAsync(preData, 0, preData.Length, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    var lzmaStream = await compressingProvider
                        .CreateCompressStreamAsync(
                            counting,
                            compressionLevel,
                            context,
                            cancellationToken
                        )
                        .ConfigureAwait(false);

                    var props = compressingProvider.GetCompressionProperties(lzmaStream, context);
                    if (props is not null)
                    {
                        await counting
                            .WriteAsync(props, 0, props.Length, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return lzmaStream;
                }
                case ZipCompressionMethod.PPMd:
                {
                    var compressingProvider = providers.GetCompressingProvider(
                        CompressionType.PPMd
                    );
                    if (compressingProvider is null)
                    {
                        throw new ArchiveOperationException("PPMd compression provider not found.");
                    }

                    var context = new CompressionContext
                    {
                        CanSeek = originalStream.CanSeek,
                        FormatOptions = writer.PpmdProperties,
                    };
                    compressionProviderHooks = compressingProvider;
                    compressionContext = context;

                    var preData = compressingProvider.GetPreCompressionData(context);
                    if (preData is not null)
                    {
                        await counting
                            .WriteAsync(preData, 0, preData.Length, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return await compressingProvider
                        .CreateCompressStreamAsync(
                            counting,
                            compressionLevel,
                            context,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                case ZipCompressionMethod.ZStandard:
                {
                    return await providers
                        .CreateCompressStreamAsync(
                            CompressionType.ZStandard,
                            counting,
                            compressionLevel,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }
                default:
                {
                    throw new NotSupportedException("CompressionMethod: " + zipCompressionMethod);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            base.Dispose(disposing);
            if (disposing)
            {
                writeStream.Dispose();

                if (limitsExceeded)
                {
                    // We have written invalid data into the archive,
                    // so we destroy it now, instead of allowing the user to continue
                    // with a defunct archive
                    originalStream.Dispose();
                    return;
                }

                WritePostCompressionData();

                var countingCount = counting?.BytesWritten ?? 0;
                entry.Crc = (uint)crc.Crc32Result;
                entry.Compressed = (ulong)countingCount;
                entry.Decompressed = decompressed;

                var zip64 =
                    entry.Compressed >= uint.MaxValue || entry.Decompressed >= uint.MaxValue;
                var compressedvalue = zip64 ? uint.MaxValue : (uint)countingCount;
                var decompressedvalue = zip64 ? uint.MaxValue : (uint)entry.Decompressed;

                if (originalStream.CanSeek)
                {
                    originalStream.Position = (long)(entry.HeaderOffset + 6);
                    originalStream.WriteByte(0);

                    if (countingCount == 0 && entry.Decompressed == 0)
                    {
                        // set compression to STORED for zero byte files (no compression data)
                        originalStream.Position = (long)(entry.HeaderOffset + 8);
                        originalStream.WriteByte(0);
                        originalStream.WriteByte(0);
                    }

                    originalStream.Position = (long)(entry.HeaderOffset + 14);

                    writer.WriteFooter(entry.Crc, compressedvalue, decompressedvalue);

                    // Ideally, we should not throw from Dispose()
                    // We should not get here as the Write call checks the limits
                    if (zip64 && entry.Zip64HeaderOffset == 0)
                    {
                        throw new NotSupportedException(
                            "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                        );
                    }

                    // If we have pre-allocated space for zip64 data,
                    // fill it out, even if it is not required
                    if (entry.Zip64HeaderOffset != 0)
                    {
                        originalStream.Position = (long)(
                            entry.HeaderOffset + entry.Zip64HeaderOffset
                        );
                        Span<byte> intBuf = stackalloc byte[8];
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                        originalStream.Write(intBuf.Slice(0, 2));
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 8 + 8);
                        originalStream.Write(intBuf.Slice(0, 2));

                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, entry.Decompressed);
                        originalStream.Write(intBuf);
                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, entry.Compressed);
                        originalStream.Write(intBuf);
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
                        throw new NotSupportedException(
                            "Streams larger than 4GiB are not supported for non-seekable streams"
                        );
                    }

                    Span<byte> intBuf = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(
                        intBuf,
                        ZipHeaderFactory.POST_DATA_DESCRIPTOR
                    );
                    originalStream.Write(intBuf);
                    writer.WriteFooter(entry.Crc, compressedvalue, decompressedvalue);
                    writer.streamPosition += (long)entry.Compressed + 16;
                }
                writer.entries.Add(entry);
            }
        }

        public override void Flush() => writeStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckWriteLimits(count);

            decompressed += (uint)count;
            crc.SlurpBlock(buffer, offset, count);
            writeStream.Write(buffer, offset, count);

            CheckPostWriteLimits();
        }

        public override async Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckWriteLimits(count);

            decompressed += (uint)count;
            crc.SlurpBlock(buffer, offset, count);
            await writeStream
                .WriteAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);

            CheckPostWriteLimits();
        }

#if !LEGACY_DOTNET
        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckWriteLimits(buffer.Length);

            decompressed += (uint)buffer.Length;
            if (MemoryMarshal.TryGetArray(buffer, out var segment))
            {
                crc.SlurpBlock(segment.Array!, segment.Offset, segment.Count);
            }
            else
            {
                var array = buffer.ToArray();
                crc.SlurpBlock(array, 0, array.Length);
            }
            await writeStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

            CheckPostWriteLimits();
        }
#endif

        private void CheckWriteLimits(int count)
        {
            // We check the limits first, because we can keep the archive consistent
            // if we can prevent the writes from happening. The compressed byte count
            // is only an estimate until compression has actually happened.
            if (entry.Zip64HeaderOffset != 0)
            {
                return;
            }

            var countingCount = counting?.BytesWritten ?? 0;
            if (
                limitsExceeded
                || ((decompressed + (uint)count) > uint.MaxValue)
                || (countingCount + (uint)count) > uint.MaxValue
            )
            {
                throw new NotSupportedException(
                    "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                );
            }
        }

        private void CheckPostWriteLimits()
        {
            if (entry.Zip64HeaderOffset != 0)
            {
                return;
            }

            var countingCount = counting?.BytesWritten ?? 0;
            if ((decompressed > uint.MaxValue) || countingCount > uint.MaxValue)
            {
                // We have written the data, so the archive is now broken. Throwing
                // here avoids throwing from Dispose(), which can mask other errors.
                limitsExceeded = true;
                throw new NotSupportedException(
                    "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                );
            }
        }

        private void WritePostCompressionData()
        {
            if (
                compressionProviderHooks is null
                || compressionContext is null
                || counting is null
                || zipCompressionMethod == ZipCompressionMethod.None
            )
            {
                return;
            }

            var postData = compressionProviderHooks.GetPostCompressionData(
                writeStream,
                compressionContext
            );
            if (postData is null || postData.Length == 0)
            {
                return;
            }

            counting.Write(postData, 0, postData.Length);
        }

        private async ValueTask WritePostCompressionDataAsync(CancellationToken cancellationToken)
        {
            if (
                compressionProviderHooks is null
                || compressionContext is null
                || counting is null
                || zipCompressionMethod == ZipCompressionMethod.None
            )
            {
                return;
            }

            var postData = compressionProviderHooks.GetPostCompressionData(
                writeStream,
                compressionContext
            );
            if (postData is null || postData.Length == 0)
            {
                return;
            }

            await counting
                .WriteAsync(postData, 0, postData.Length, cancellationToken)
                .ConfigureAwait(false);
        }

#if NET48 || NETSTANDARD2_0
        public async ValueTask DisposeAsync()
#else
        public override async ValueTask DisposeAsync()
#endif
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            if (writeStream is IAsyncDisposable asyncDisposableWriteStream)
            {
                await asyncDisposableWriteStream.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                writeStream.Dispose();
            }

            if (limitsExceeded)
            {
                // We have written invalid data into the archive, so destroy it
                if (originalStream is IAsyncDisposable asyncDisposableOriginalStream)
                {
                    await asyncDisposableOriginalStream.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    originalStream.Dispose();
                }
                return;
            }

            await WritePostCompressionDataAsync(CancellationToken.None).ConfigureAwait(false);

            var countingCount = counting?.BytesWritten ?? 0;
            entry.Crc = (uint)crc.Crc32Result;
            entry.Compressed = (ulong)countingCount;
            entry.Decompressed = decompressed;

            var zip64 = entry.Compressed >= uint.MaxValue || entry.Decompressed >= uint.MaxValue;
            var compressedvalue = zip64 ? uint.MaxValue : (uint)countingCount;
            var decompressedvalue = zip64 ? uint.MaxValue : (uint)entry.Decompressed;

            if (originalStream.CanSeek)
            {
                originalStream.Position = (long)(entry.HeaderOffset + 6);
                await originalStream.WriteAsync(new byte[] { 0 }, 0, 1).ConfigureAwait(false);

                if (countingCount == 0 && entry.Decompressed == 0)
                {
                    // set compression to STORED for zero byte files
                    originalStream.Position = (long)(entry.HeaderOffset + 8);
                    await originalStream
                        .WriteAsync(new byte[] { 0, 0 }, 0, 2)
                        .ConfigureAwait(false);
                }

                originalStream.Position = (long)(entry.HeaderOffset + 14);

                await WriteFooterAsync(
                        originalStream,
                        entry.Crc,
                        compressedvalue,
                        decompressedvalue
                    )
                    .ConfigureAwait(false);

                if (zip64 && entry.Zip64HeaderOffset == 0)
                {
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }

                if (entry.Zip64HeaderOffset != 0)
                {
                    originalStream.Position = (long)(entry.HeaderOffset + entry.Zip64HeaderOffset);
                    var intBuf = new byte[8];
                    BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                    await originalStream.WriteAsync(intBuf, 0, 2).ConfigureAwait(false);
                    BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 8 + 8);
                    await originalStream.WriteAsync(intBuf, 0, 2).ConfigureAwait(false);

                    BinaryPrimitives.WriteUInt64LittleEndian(intBuf, entry.Decompressed);
                    await originalStream.WriteAsync(intBuf, 0, 8).ConfigureAwait(false);
                    BinaryPrimitives.WriteUInt64LittleEndian(intBuf, entry.Compressed);
                    await originalStream.WriteAsync(intBuf, 0, 8).ConfigureAwait(false);
                }

                originalStream.Position = writer.streamPosition + (long)entry.Compressed;
                writer.streamPosition += (long)entry.Compressed;
            }
            else
            {
                if (zip64)
                {
                    throw new NotSupportedException(
                        "Streams larger than 4GiB are not supported for non-seekable streams"
                    );
                }

                var intBuf = new byte[4];
                BinaryPrimitives.WriteUInt32LittleEndian(
                    intBuf,
                    ZipHeaderFactory.POST_DATA_DESCRIPTOR
                );
                await originalStream.WriteAsync(intBuf, 0, 4).ConfigureAwait(false);
                await WriteFooterAsync(
                        originalStream,
                        entry.Crc,
                        compressedvalue,
                        decompressedvalue
                    )
                    .ConfigureAwait(false);
                writer.streamPosition += (long)entry.Compressed + 16;
            }
            writer.entries.Add(entry);
#if !NET48 && !NETSTANDARD2_0
            // base.DisposeAsync() is a no-op since isDisposed is already set
            await base.DisposeAsync().ConfigureAwait(false);
#endif
        }

        private static async ValueTask WriteFooterAsync(
            Stream stream,
            uint crc,
            uint compressed,
            uint uncompressed
        )
        {
            var buf = new byte[12];
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0), crc);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(4), compressed);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(8), uncompressed);
            await stream.WriteAsync(buf, 0, buf.Length).ConfigureAwait(false);
        }
    }
}
