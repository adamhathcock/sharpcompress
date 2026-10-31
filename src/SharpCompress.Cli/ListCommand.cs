using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Readers;

namespace SharpCompress.Cli;

internal static class ListCommand
{
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
        var entries = new List<object>();
        long index = 0;
        async Task AddAsync(object entry)
        {
            if (ndjson)
            {
                await OutputRenderer.RenderAsync(entry, output, false, true).ConfigureAwait(false);
            }
            else if (json)
            {
                entries.Add(entry);
            }
            else if (entry is string text)
            {
                await output.WriteLineAsync(text).ConfigureAwait(false);
            }
            else
            {
                await OutputRenderer.RenderAsync(entry, output, false, false).ConfigureAwait(false);
            }
        }
        async Task AddEntryAsync(IEntry entry)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = EntryInformation.FromEntry(entry);
            var ordinal = index++;
            await AddAsync(
                    longListing || json || ndjson
                        ? new { Index = ordinal, Metadata = snapshot }
                        : snapshot.Key ?? "(unnamed)"
                )
                .ConfigureAwait(false);
        }
        if (detection.Archive is null)
        {
            await AddAsync(
                    new
                    {
                        Index = index++,
                        Kind = "logicalPayload",
                        Name = System.IO.Path.GetFileNameWithoutExtension(path),
                        NameSource = "inferred",
                        Size = (long?)null,
                        Compression = detection.Compression,
                    }
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
                .RenderAsync(
                    new { Entries = entries, Integrity = "notTested" },
                    output,
                    true,
                    false
                )
                .ConfigureAwait(false);
        }

        return 0;
    }
}
