using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Compressors.Rar;

namespace SharpCompress.Readers.Rar;

public abstract partial class RarReader
{
    /// <summary>
    /// Returns file parts asynchronously for the current entry.
    /// Used for async stream operations in solid RAR archives.
    /// </summary>
    protected virtual IAsyncEnumerable<FilePart> CreateFilePartEnumerableForCurrentEntryAsync() =>
        Entry.Parts.ToAsyncEnumerable();

    /// <summary>
    /// Asynchronously creates an entry stream for the current entry.
    /// Supports both RAR v3 and v5 archives with proper CRC verification.
    /// </summary>
    protected override async ValueTask<EntryStream> GetEntryStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (Entry.IsRedir)
        {
            throw new ArchiveOperationException("no stream for redirect entry");
        }

        var stream = await MultiVolumeReadOnlyAsyncStream
            .Create(CreateFilePartEnumerableForCurrentEntryAsync().CastAsync<RarFilePart>())
            .ConfigureAwait(false);
        if (Entry.IsRarV3)
        {
            return CreateEntryStream(
                await RarCrcStream
                    .CreateAsync(UnpackV1.Value, Entry.FileHeader, stream, cancellationToken)
                    .ConfigureAwait(false)
            );
        }

        if (Entry.FileHeader.FileCrc?.Length > 5)
        {
            return CreateEntryStream(
                await RarBLAKE2spStream
                    .CreateAsync(UnpackV2017.Value, Entry.FileHeader, stream, cancellationToken)
                    .ConfigureAwait(false)
            );
        }

        return CreateEntryStream(
            await RarCrcStream
                .CreateAsync(UnpackV2017.Value, Entry.FileHeader, stream, cancellationToken)
                .ConfigureAwait(false)
        );
    }
}
