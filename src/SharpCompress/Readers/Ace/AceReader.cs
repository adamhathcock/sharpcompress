using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Ace;
using SharpCompress.Common.Ace.Headers;

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
public abstract partial class AceReader : AbstractReader<AceEntry, AceVolume>
{
    private readonly IArchiveEncoding _archiveEncoding;

    internal AceReader(ReaderOptions options)
        : base(options, ArchiveType.Ace)
    {
        _archiveEncoding = Options.ArchiveEncoding;
    }

    /// <summary>
    /// Derived class must create or manage the Volume itself.
    /// AbstractReader.Volume is get-only, so it cannot be set here.
    /// </summary>
    public override AceVolume? Volume => _volume;

    private AceVolume? _volume;

    protected abstract void ValidateArchive(AceVolume archive);

    protected override IEnumerable<AceEntry> GetEntries(Stream stream)
    {
        if (_volume == null)
        {
            _volume = new AceVolume(stream, Options, 0);
            ValidateArchive(_volume);
        }

        var mainHeaderReader = new AceMainHeader(_archiveEncoding);
        var mainHeader = mainHeaderReader.Read(stream);
        if (mainHeader == null)
        {
            yield break;
        }

        if (mainHeader.IsMultiVolume)
        {
            throw new MultiVolumeExtractionException("Multi volumes are currently not supported");
        }

        var localHeaderReader = new AceFileHeader(_archiveEncoding);
        while (true)
        {
            var localHeader = localHeaderReader.Read(stream);
            if (localHeader?.IsFileEncrypted == true)
            {
                throw new CryptographicException(
                    "Password protected archives are currently not supported"
                );
            }
            if (localHeader == null)
            {
                break;
            }

            yield return new AceEntry(new AceFilePart((AceFileHeader)localHeader, stream));
        }
    }

    protected override async IAsyncEnumerable<AceEntry> GetEntriesAsync(Stream stream)
    {
        var mainHeaderReader = new AceMainHeader(_archiveEncoding);
        var mainHeader = await mainHeaderReader.ReadAsync(stream);
        if (mainHeader == null)
        {
            yield break;
        }

        if (mainHeader?.IsMultiVolume == true)
        {
            throw new MultiVolumeExtractionException("Multi volumes are currently not supported");
        }

        if (_volume == null)
        {
            _volume = new AceVolume(stream, Options, 0);
            ValidateArchive(_volume);
        }

        var localHeaderReader = new AceFileHeader(_archiveEncoding);
        while (true)
        {
            var localHeader = await localHeaderReader.ReadAsync(stream);
            if (localHeader?.IsFileEncrypted == true)
            {
                throw new CryptographicException(
                    "Password protected archives are currently not supported"
                );
            }
            if (localHeader == null)
            {
                break;
            }

            yield return new AceEntry(new AceFilePart((AceFileHeader)localHeader, stream));
        }
    }

    protected virtual IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry() =>
        Entry.Parts;
}
