using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.GZip;
using SharpCompress.Readers;
using SharpCompress.Readers.GZip;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Archives.GZip
{
    public class GZipArchive : AbstractWritableArchive<GZipArchiveEntry, GZipVolume>
    {
        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="readerOptions"></param>
        public static GZipArchive Open(string filePath, ReaderOptions? readerOptions = null)
        {
            filePath.CheckNotNullOrEmpty(nameof(filePath));
            return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="readerOptions"></param>
        public static GZipArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null,
                                       CancellationToken cancellationToken = default)
        {
            fileInfo.CheckNotNull(nameof(fileInfo));
            return new GZipArchive(fileInfo, readerOptions ?? new ReaderOptions(), cancellationToken);
        }

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        public static GZipArchive Open(Stream stream, ReaderOptions? readerOptions = null,
                                       CancellationToken cancellationToken = default)
        {
            stream.CheckNotNull(nameof(stream));
            return new GZipArchive(stream, readerOptions ?? new ReaderOptions(), cancellationToken);
        }

        public static GZipArchive Create()
        {
            return new GZipArchive();
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="options"></param>
        internal GZipArchive(FileInfo fileInfo, ReaderOptions options,
                             CancellationToken cancellationToken)
            : base(ArchiveType.GZip, fileInfo, options, cancellationToken)
        {
        }

        protected override IAsyncEnumerable<GZipVolume> LoadVolumes(FileInfo file,
                                                                    CancellationToken cancellationToken)
        {
            return  new GZipVolume(file, ReaderOptions).AsAsyncEnumerable();
        }

        public static ValueTask<bool> IsGZipFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return IsGZipFileAsync(new FileInfo(filePath), cancellationToken);
        }

        public static async ValueTask<bool> IsGZipFileAsync(FileInfo fileInfo, CancellationToken cancellationToken = default)
        {
            if (!fileInfo.Exists)
            {
                return false;
            }

            await using Stream stream = fileInfo.OpenRead();
            return await IsGZipFileAsync(stream, cancellationToken);
        }

        public Task SaveToAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return SaveToAsync(new FileInfo(filePath), cancellationToken);
        }

        public async Task SaveToAsync(FileInfo fileInfo, CancellationToken cancellationToken = default)
        {
            await using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
            await SaveToAsync(stream, new WriterOptions(CompressionType.GZip), cancellationToken);
        }

        public static async ValueTask<bool> IsGZipFileAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            // read the header on the first read
            using var header = MemoryPool<byte>.Shared.Rent(10);

            // workitem 8501: handle edge case (decompress empty stream)
            if (!await stream.ReadFullyAsync(header.Memory.Slice(0, 10), cancellationToken))
            {
                return false;
            }

            if (header.Memory.Span[0] != 0x1F || header.Memory.Span[1] != 0x8B || header.Memory.Span[2] != 8)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Takes multiple seekable Streams for a multi-part archive
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="options"></param>
        internal GZipArchive(Stream stream, ReaderOptions options,
                             CancellationToken cancellationToken)
            : base(ArchiveType.GZip, stream, options, cancellationToken)
        {
        }

        internal GZipArchive()
            : base(ArchiveType.GZip)
        {
        }

        protected override async ValueTask<GZipArchiveEntry> CreateEntryInternal(string filePath, Stream source, long size, DateTime? modified,
                                                                bool closeStream, CancellationToken cancellationToken = default)
        {
            if (await Entries.AnyAsync(cancellationToken: cancellationToken))
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }
            return new GZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);
        }

        protected override async ValueTask SaveToAsync(Stream stream, WriterOptions options,
                                            IAsyncEnumerable<GZipArchiveEntry> oldEntries,
                                            IAsyncEnumerable<GZipArchiveEntry> newEntries, 
                                            CancellationToken cancellationToken = default)
        {
            if (await Entries.CountAsync(cancellationToken: cancellationToken) > 1)
            {
                throw new InvalidOperationException("Only one entry is allowed in a GZip Archive");
            }

            await using var writer = new GZipWriter(stream, new GZipWriterOptions(options));
            await foreach (var entry in oldEntries.Concat(newEntries)
                                                  .Where(x => !x.IsDirectory)
                                                  .WithCancellation(cancellationToken))
            {
                await using var entryStream = entry.OpenEntryStream();
                await writer.WriteAsync(entry.Key, entryStream, entry.LastModifiedTime, cancellationToken);
            }
        }

        protected override async IAsyncEnumerable<GZipVolume> LoadVolumes(IAsyncEnumerable<Stream> streams,
                                                                          [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            yield return new GZipVolume(await streams.FirstAsync(cancellationToken: cancellationToken), ReaderOptions);
        }

        protected override async IAsyncEnumerable<GZipArchiveEntry> LoadEntries(IAsyncEnumerable<GZipVolume> volumes,
                                                                                [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            Stream stream = (await volumes.SingleAsync(cancellationToken: cancellationToken)).Stream;
            var part = new GZipFilePart(ReaderOptions.ArchiveEncoding);
            await part.Initialize(stream, cancellationToken);
            yield return new GZipArchiveEntry(this, part);
        }

        protected override async ValueTask<IReader> CreateReaderForSolidExtraction()
        {
            var stream = (await Volumes.SingleAsync()).Stream;
            stream.Position = 0;
            return GZipReader.Open(stream);
        }
    }
}
