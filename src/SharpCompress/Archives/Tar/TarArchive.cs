using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar
{
    public class TarArchive : AbstractWritableArchive<TarArchiveEntry, TarVolume>
    {
        /// <summary>
        /// Constructor expects a filepath to an existing file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="readerOptions"></param>
        public static TarArchive Open(string filePath, ReaderOptions? readerOptions = null)
        {
            filePath.CheckNotNullOrEmpty(nameof(filePath));
            return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="readerOptions"></param>
        public static TarArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null,
                                      CancellationToken cancellationToken = default)
        {
            fileInfo.CheckNotNull(nameof(fileInfo));
            return new TarArchive(fileInfo, readerOptions ?? new ReaderOptions(), cancellationToken);
        }

        /// <summary>
        /// Takes a seekable Stream as a source
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        public static TarArchive Open(Stream stream, ReaderOptions? readerOptions = null,
                    CancellationToken cancellationToken = default)
        {
            stream.CheckNotNull(nameof(stream));
            return new TarArchive(stream, readerOptions ?? new ReaderOptions(), cancellationToken);
        }

        public static ValueTask<bool> IsTarFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return IsTarFileAsync(new FileInfo(filePath), cancellationToken);
        }

        public static async ValueTask<bool> IsTarFileAsync(FileInfo fileInfo, CancellationToken cancellationToken = default)
        {
            if (!fileInfo.Exists)
            {
                return false;
            }

            await using Stream stream = fileInfo.OpenRead();
            return await IsTarFileAsync(stream, cancellationToken);
        }

        public static async ValueTask<bool> IsTarFileAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                TarHeader tarHeader = new(new ArchiveEncoding());
                bool readSucceeded = await tarHeader.Read(stream, cancellationToken);
                bool isEmptyArchive = tarHeader.Name.Length == 0 && tarHeader.Size == 0 && Enum.IsDefined(typeof(EntryType), tarHeader.EntryType);
                return readSucceeded || isEmptyArchive;
            }
            catch
            {
            }
            return false;
        }

        /// <summary>
        /// Constructor with a FileInfo object to an existing file.
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="readerOptions"></param>
        internal TarArchive(FileInfo fileInfo, ReaderOptions readerOptions,
                            CancellationToken cancellationToken)
            : base(ArchiveType.Tar, fileInfo, readerOptions, cancellationToken)
        {
        }

        protected override IAsyncEnumerable<TarVolume> LoadVolumes(FileInfo file, CancellationToken cancellationToken)
        {
            return new TarVolume(file.OpenRead(), ReaderOptions).AsAsyncEnumerable();
        }

        /// <summary>
        /// Takes multiple seekable Streams for a multi-part archive
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="readerOptions"></param>
        internal TarArchive(Stream stream, ReaderOptions readerOptions,
                            CancellationToken cancellationToken)
            : base(ArchiveType.Tar, stream, readerOptions, cancellationToken)
        {
        }

        internal TarArchive()
            : base(ArchiveType.Tar)
        {
        }

        protected override async IAsyncEnumerable<TarVolume> LoadVolumes(IAsyncEnumerable<Stream> streams, 
                                                                         [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            yield return new TarVolume(await streams.FirstAsync(cancellationToken: cancellationToken), ReaderOptions);
        }

        protected override async IAsyncEnumerable<TarArchiveEntry> LoadEntries(IAsyncEnumerable<TarVolume> volumes, 
                                                                               [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            Stream stream = (await volumes.SingleAsync(cancellationToken: cancellationToken)).Stream;
            TarHeader? previousHeader = null;
            await foreach (TarHeader? header in TarHeaderFactory.ReadHeader(StreamingMode.Seekable, stream, ReaderOptions.ArchiveEncoding, cancellationToken))
            {
                if (header != null)
                {
                    if (header.EntryType == EntryType.LongName)
                    {
                        previousHeader = header;
                    }
                    else
                    {
                        if (previousHeader != null)
                        {
                            var entry = new TarArchiveEntry(this, new TarFilePart(previousHeader, stream),
                                                            CompressionType.None);

                            var oldStreamPos = stream.Position;

                            using (var entryStream = entry.OpenEntryStream())
                            {
                                using (var memoryStream = new MemoryStream())
                                {
                                    entryStream.TransferTo(memoryStream);
                                    memoryStream.Position = 0;
                                    var bytes = memoryStream.ToArray();

                                    header.Name = ReaderOptions.ArchiveEncoding.Decode(bytes).TrimNulls();
                                }
                            }

                            stream.Position = oldStreamPos;

                            previousHeader = null;
                        }
                        yield return new TarArchiveEntry(this, new TarFilePart(header, stream), CompressionType.None);
                    }
                }
            }
        }

        public static TarArchive Create()
        {
            return new();
        }

        protected override ValueTask<TarArchiveEntry> CreateEntryInternal(string filePath, Stream source,
                                                               long size, DateTime? modified, bool closeStream,
                                                               CancellationToken cancellationToken)
        {
            return new (new TarWritableArchiveEntry(this, source, CompressionType.Unknown, filePath, size, modified,
                                               closeStream));
        }

        protected override async ValueTask SaveToAsync(Stream stream, WriterOptions options,
                                                       IAsyncEnumerable<TarArchiveEntry> oldEntries,
                                                       IAsyncEnumerable<TarArchiveEntry> newEntries,
                                                       CancellationToken cancellationToken = default)
        {
            await using var writer = new TarWriter(stream, new TarWriterOptions(options));
            await foreach (var entry in oldEntries.Concat(newEntries)
                                                  .Where(x => !x.IsDirectory)
                                                  .WithCancellation(cancellationToken))
            {
                await using var entryStream = entry.OpenEntryStream();
                await writer.WriteAsync(entry.Key, entryStream, entry.LastModifiedTime, cancellationToken);
            }
        }

        protected override async ValueTask<IReader> CreateReaderForSolidExtraction()
        {
            var stream = (await Volumes.SingleAsync()).Stream;
            stream.Position = 0;
            return await TarReader.OpenAsync(stream);
        }
    }
}
