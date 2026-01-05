using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Readers.Zip;

public class ZipReader : AbstractReader<ZipEntry, ZipVolume>
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
    /// <param name="options"></param>
    /// <returns></returns>
    public static ZipReader Open(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        return new ZipReader(stream, options ?? new ReaderOptions());
    }

    public static ZipReader Open(
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
                                new StreamingZipFilePart((LocalEntryHeader)h, stream)
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
}
