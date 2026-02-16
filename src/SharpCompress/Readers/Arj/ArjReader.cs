using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arj;
using SharpCompress.Common.Arj.Headers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Readers.Arj;

public abstract partial class ArjReader : AbstractReader<ArjEntry, ArjVolume>
{
    internal ArjReader(ReaderOptions options)
        : base(options, ArchiveType.Arj) { }

    /// <summary>
    /// Derived class must create or manage the Volume itself.
    /// AbstractReader.Volume is get-only, so it cannot be set here.
    /// </summary>
    public override ArjVolume? Volume => _volume;

    private ArjVolume? _volume;

    /// <summary>
    /// Opens an ArjReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.NotNull(nameof(stream));
        return new SingleVolumeArjReader(stream, readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Opens an ArjReader for Non-seeking usage with multiple volumes
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader OpenReader(IEnumerable<Stream> streams, ReaderOptions? options = null)
    {
        streams.NotNull(nameof(streams));
        return new MultiVolumeArjReader(streams, options ?? new ReaderOptions());
    }

    protected abstract void ValidateArchive(ArjVolume archive);

    protected override IEnumerable<ArjEntry> GetEntries(Stream stream)
    {
        var encoding = new ArchiveEncoding();
        var mainHeaderReader = new ArjMainHeader(encoding);
        var localHeaderReader = new ArjLocalHeader(encoding);

        var mainHeader = mainHeaderReader.Read(stream);
        if (mainHeader?.IsVolume == true)
        {
            throw new MultiVolumeExtractionException("Multi volumes are currently not supported");
        }
        if (mainHeader?.IsGabled == true)
        {
            throw new CryptographicException(
                "Password protected archives are currently not supported"
            );
        }

        if (_volume == null)
        {
            _volume = new ArjVolume(stream, Options, 0);
            ValidateArchive(_volume);
        }

        while (true)
        {
            var localHeader = localHeaderReader.Read(stream);
            if (localHeader == null)
            {
                break;
            }

            // Skip non-file headers (like CommentHeader)
            if (
                localHeader.FileType != FileType.Binary
                && localHeader.FileType != FileType.Text7Bit
            )
            {
                continue;
            }

            yield return new ArjEntry(
                new ArjFilePart((ArjLocalHeader)localHeader, stream),
                Options
            );
        }
    }

    protected override async IAsyncEnumerable<ArjEntry> GetEntriesAsync(Stream stream)
    {
        var encoding = new ArchiveEncoding();
        var mainHeaderReader = new ArjMainHeader(encoding);
        var localHeaderReader = new ArjLocalHeader(encoding);

        var mainHeader = await mainHeaderReader.ReadAsync(stream).ConfigureAwait(false);
        if (mainHeader?.IsVolume == true)
        {
            throw new MultiVolumeExtractionException("Multi volumes are currently not supported");
        }
        if (mainHeader?.IsGabled == true)
        {
            throw new CryptographicException(
                "Password protected archives are currently not supported"
            );
        }

        if (_volume == null)
        {
            _volume = new ArjVolume(stream, Options, 0);
            ValidateArchive(_volume);
        }

        while (true)
        {
            var localHeader = await localHeaderReader.ReadAsync(stream).ConfigureAwait(false);
            if (localHeader == null)
            {
                break;
            }

            // Skip non-file headers (like CommentHeader)
            if (
                localHeader.FileType != FileType.Binary
                && localHeader.FileType != FileType.Text7Bit
            )
            {
                continue;
            }

            yield return new ArjEntry(
                new ArjFilePart((ArjLocalHeader)localHeader, stream),
                Options
            );
        }
    }

    protected virtual IEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntry() =>
        Entry.Parts;
}
