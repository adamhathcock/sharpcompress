using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Ace;

namespace SharpCompress.Readers.Ace;

/// <summary>
/// Reader for ACE archives.
/// ACE is a proprietary archive format. This implementation supports version 1 archives
/// and can extract uncompressed (stored) entries. Compressed entries require proprietary
/// decompression algorithms that are not publicly documented.
/// </summary>
public class AceReader : AbstractReader<AceEntry, AceVolume>
{
    private readonly AceEntryHeader _headerReader;
    private bool _mainHeaderRead;

    private AceReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.Ace)
    {
        Volume = new AceVolume(stream, options, 0);
        _headerReader = new AceEntryHeader(Options.ArchiveEncoding);
    }

    public override AceVolume Volume { get; }

    /// <summary>
    /// Opens an AceReader for non-seeking usage with a single volume.
    /// </summary>
    /// <param name="stream">The stream containing the ACE archive.</param>
    /// <param name="options">Reader options.</param>
    /// <returns>An AceReader instance.</returns>
    public static AceReader Open(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        return new AceReader(stream, options ?? new ReaderOptions());
    }

    protected override IEnumerable<AceEntry> GetEntries(Stream stream)
    {
        // First, skip past the main header if we haven't already
        if (!_mainHeaderRead)
        {
            if (!_headerReader.ReadMainHeader(stream))
            {
                yield break;
            }
            _mainHeaderRead = true;
        }

        // Read file entries
        AceEntryHeader headerReader = new AceEntryHeader(Options.ArchiveEncoding);
        AceEntryHeader? header;
        while ((header = headerReader.ReadHeader(stream)) != null)
        {
            yield return new AceEntry(new AceFilePart(header, stream));
        }
    }
}
