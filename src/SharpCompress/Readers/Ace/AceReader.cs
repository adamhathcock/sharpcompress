using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Ace;

namespace SharpCompress.Readers.Ace;

/// <summary>
/// Reader for ACE archives.
/// ACE is a proprietary archive format. This implementation supports both ACE 1.0 and ACE 2.0 formats
/// and can read archive metadata and extract uncompressed (stored) entries.
/// Compressed entries require proprietary decompression algorithms that are not publicly documented.
/// </summary>
/// <remarks>
/// ACE 2.0 additions over ACE 1.0:
/// - Improved LZ77 compression (compression type 2)
/// - Recovery record support
/// - Additional header flags
/// </remarks>
public class AceReader : AbstractReader<AceEntry, AceVolume>
{
    private readonly AceEntryHeader _mainHeaderReader;
    private bool _mainHeaderRead;

    private AceReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.Ace)
    {
        Volume = new AceVolume(stream, options, 0);
        _mainHeaderReader = new AceEntryHeader(Options.ArchiveEncoding);
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
            if (!_mainHeaderReader.ReadMainHeader(stream))
            {
                yield break;
            }
            _mainHeaderRead = true;
        }

        // Read file entries - create new header reader for each entry
        // since ReadHeader modifies the object state
        AceEntryHeader entryHeaderReader = new AceEntryHeader(Options.ArchiveEncoding);
        AceEntryHeader? header;
        while ((header = entryHeaderReader.ReadHeader(stream)) != null)
        {
            yield return new AceEntry(new AceFilePart(header, stream));
        }
    }
}
