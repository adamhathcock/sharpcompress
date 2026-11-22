using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Common.Zip;
using SharpCompress.Detection;
using SharpCompress.Readers;

namespace SharpCompress.Cli;

internal static class ListCommand
{
    private static TarMetadata ReadTarMetadata(TarEntry entry) =>
        new(
            // TarEntry.Mode adds a legacy directory marker indistinguishable from the sticky bit.
            !entry.IsDirectory && entry.Mode is long mode
                ? Convert.ToString(mode, 8).PadLeft(4, '0')
                : null,
            entry.UserId,
            entry.GroupId
        );

    internal static async Task<int> ExecuteAsync(
        string path,
        FileDetection detection,
        ReaderOptions options,
        TextWriter output,
        bool json,
        bool ndjson,
        bool longListing,
        CancellationToken cancellationToken
    )
    {
        var entries = new List<EntryRow>();
        long index = 0;
        async Task AddAsync(EntryRow entry)
        {
            if (ndjson)
            {
                await OutputRenderer.RenderAsync(entry, output, false, true).ConfigureAwait(false);
            }
            else if (json)
            {
                entries.Add(entry);
            }
            else
            {
                await OutputRenderer.RenderAsync(entry, output, false, false).ConfigureAwait(false);
            }
        }
        async Task AddEntryAsync(IEntry entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ordinal = index++;
            if (!longListing && !json && !ndjson)
            {
                await OutputRenderer.WriteEntryNameAsync(output, entry.Key).ConfigureAwait(false);
                return;
            }
            await AddAsync(
                    new EntryRow(
                        ordinal,
                        EntryInformation.FromEntry(entry),
                        entry is TarEntry tar ? ReadTarMetadata(tar) : null,
                        entry is ZipEntry { Comment: { Length: > 0 } comment }
                            ? new ZipMetadata(comment)
                            : null
                    )
                )
                .ConfigureAwait(false);
        }
        if (detection.Archive is null)
        {
            await AddAsync(
                    new EntryRow(
                        index++,
                        Kind: "logicalPayload",
                        Name: System.IO.Path.GetFileNameWithoutExtension(path),
                        NameSource: "inferred",
                        Compression: detection.Compression
                    )
                )
                .ConfigureAwait(false);
        }
        else if (detection.Archive.SupportedApis.HasFlag(ArchiveAccessMode.Archive))
        {
            await using var archive = await ArchiveFactory
                .OpenAsyncArchive(path, options, cancellationToken)
                .ConfigureAwait(false);
            await foreach (
                var entry in archive
                    .EntriesAsync.WithCancellation(cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                await AddEntryAsync(entry).ConfigureAwait(false);
            }
        }
        else
        {
            await using var reader = await ReaderFactory
                .OpenAsyncReader(path, options, cancellationToken)
                .ConfigureAwait(false);
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                await AddEntryAsync(reader.Entry).ConfigureAwait(false);
            }
        }
        if (json)
        {
            await OutputRenderer
                .RenderAsync(new ListResult(entries), output, true, false)
                .ConfigureAwait(false);
        }

        return 0;
    }
}
