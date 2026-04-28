using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Writers.Zip;

public partial class ZipWriter
{
    /// <summary>
    /// Asynchronously disposes the writer, writing the ZIP central directory and end record.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        GC.SuppressFinalize(this);
        _isDisposed = true;

        // Buffer the entire central directory + end record into memory, then write async.
        // This avoids synchronous writes to the underlying stream during finalization.
        using var ms = new MemoryStream();
        ulong size = 0;
        foreach (var entry in entries)
        {
            size += entry.Write(ms);
        }
        WriteEndRecord(ms, size);
        ms.Position = 0;
        await ms.CopyToAsync(OutputStream.NotNull()).ConfigureAwait(false);

        OutputStream?.Dispose();
        // base.DisposeAsync() is a no-op since _isDisposed is already set
        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes an entry to the ZIP archive.
    /// </summary>
    public override async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await WriteAsync(
                filename,
                source,
                new ZipWriterEntryOptions { ModificationDateTime = modificationTime },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes an entry to the ZIP archive with specified options.
    /// </summary>
    public async ValueTask WriteAsync(
        string entryPath,
        Stream source,
        ZipWriterEntryOptions zipWriterEntryOptions,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var output = await WriteToStreamAsync(
                entryPath,
                zipWriterEntryOptions,
                cancellationToken
            )
            .ConfigureAwait(false);
        var progressStream = WrapWithProgress(source, entryPath);
        await progressStream.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ZipWritingStream> WriteToStreamAsync(
        string entryPath,
        ZipWriterEntryOptions options,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var compression = ToZipCompressionMethod(options.CompressionType ?? compressionType);
        var entry = new ZipCentralDirectoryEntry(
            compression,
            entryPath,
            (ulong)streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime,
        };

        var useZip64 = isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        var headersize = (uint)
            await WriteHeaderAsync(entryPath, options, entry, useZip64, cancellationToken)
                .ConfigureAwait(false);
        streamPosition += headersize;
        return new ZipWritingStream(
            this,
            OutputStream.NotNull(),
            entry,
            compression,
            options.CompressionLevel ?? compressionLevel
        );
    }

    private async Task<int> WriteHeaderAsync(
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64,
        CancellationToken cancellationToken
    )
    {
        // Build the header synchronously into a MemoryStream, then async-copy to OutputStream.
        // This avoids any synchronous writes to the potentially async-only output stream.
        using var ms = new MemoryStream();
        var result = WriteHeader(ms, filename, zipWriterEntryOptions, entry, useZip64);
        ms.Position = 0;
        await ms.CopyToAsync(OutputStream.NotNull(), 81920, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Asynchronously writes a directory entry to the ZIP archive.
    /// Uses synchronous implementation for directory entries as they are lightweight.
    /// </summary>
    public override async ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteDirectory(directoryName, modificationTime);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
