using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Readers.Zip;

public partial class ZipReader : AbstractReader<ZipEntry, ZipVolume>
{
    private readonly StreamingZipHeaderFactory _headerFactory;

    private ZipReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.Zip)
    {
        Volume = new ZipVolume(stream, options);
        _headerFactory = new StreamingZipHeaderFactory(
            options.Password,
            options.ArchiveEncoding,
            null
        );
    }

    private ZipReader(Stream stream, ReaderOptions options, IEnumerable<ZipEntry> entries)
        : base(options, ArchiveType.Zip)
    {
        Volume = new ZipVolume(stream, options);
        _headerFactory = new StreamingZipHeaderFactory(
            options.Password,
            options.ArchiveEncoding,
            entries
        );
    }

    public override ZipVolume Volume { get; }

    #region Open

    /// <summary>
    /// Opens a ZipReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        return new ZipReader(stream, readerOptions ?? new ReaderOptions());
    }

    public static IReader OpenReader(
        Stream stream,
        ReaderOptions? options,
        IEnumerable<ZipEntry> entries
    )
    {
        stream.NotNull(nameof(stream));
        return new ZipReader(stream, options ?? new ReaderOptions(), entries);
    }

    #endregion Open

    protected override IEnumerable<ZipEntry> GetEntries(Stream stream)
    {
        foreach (var h in _headerFactory.ReadStreamHeader(stream))
        {
            if (h != null)
            {
                switch (h.ZipHeaderType)
                {
                    case ZipHeaderType.LocalEntry:
                        {
                            yield return new ZipEntry(
                                new StreamingZipFilePart(
                                    (LocalEntryHeader)h,
                                    stream,
                                    Options.Providers
                                ),
                                Options
                            );
                        }
                        break;
                    case ZipHeaderType.DirectoryEntry:
                        // DirectoryEntry headers in the central directory are intentionally skipped.
                        // In streaming mode, we can only read forward, and DirectoryEntry headers
                        // reference LocalEntry headers that have already been processed. The file
                        // data comes from LocalEntry headers, not DirectoryEntry headers.
                        // For multi-volume ZIPs where file data spans multiple files, use ZipArchive
                        // instead, which requires seekable streams.
                        break;
                    case ZipHeaderType.DirectoryEnd:
                    {
                        yield break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns entries asynchronously for streams that only support async reads.
    /// </summary>
    protected override IAsyncEnumerable<ZipEntry> GetEntriesAsync(Stream stream) =>
        new ZipEntryAsyncEnumerable(_headerFactory, stream, Options);

    // Async nested classes moved to ZipReader.Async.cs
}
