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

        if (OutputStream is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            OutputStream?.Dispose();
        }
        // base.DisposeAsync() is a no-op since _isDisposed is already set
        await base.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an entry to the ZIP archive.
    /// </summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
#if !SYNC_ONLY
        cancellationToken.ThrowIfCancellationRequested();
#endif
        await WriteAsync(
                filename,
                source,
                new ZipWriterEntryOptions { ModificationDateTime = modificationTime },
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an entry to the ZIP archive with specified options.
    /// </summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public async ValueTask WriteAsync(
        string entryPath,
        Stream source,
        ZipWriterEntryOptions zipWriterEntryOptions,
        CancellationToken cancellationToken = default
    )
    {
#if !SYNC_ONLY
        cancellationToken.ThrowIfCancellationRequested();
#endif
        await using var output = await WriteToStreamAsync(
                entryPath,
                zipWriterEntryOptions,
                cancellationToken
            )
            .ConfigureAwait(false);
        var progressStream = WrapWithProgress(source, entryPath);
        await progressStream
            .CopyToAsync(output, WriterOptions.BufferSize, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<ZipWritingStream> WriteToStreamAsync(
        string entryPath,
        ZipWriterEntryOptions options,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        options.ValidateWithFallback(compressionType, compressionLevel);
        var compression = ToZipCompressionMethod(options.CompressionType ?? compressionType);

        entryPath = NormalizeFilename(entryPath);
        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;
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
            await WriteHeaderAsync(
                    entryPath,
                    options,
                    entry,
                    useZip64,
                    usesDataDescriptor: !OutputStream.NotNull().CanSeek,
                    cancellationToken
                )
                .ConfigureAwait(false);
        streamPosition += headersize;
        return await ZipWritingStream
            .CreateAsync(
                this,
                OutputStream.NotNull(),
                entry,
                compression,
                options.CompressionLevel ?? compressionLevel,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async ValueTask<int> WriteHeaderAsync(
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64,
        bool usesDataDescriptor,
        CancellationToken cancellationToken
    )
    {
        // Build the header synchronously into a MemoryStream, then async-copy to OutputStream.
        // This avoids any synchronous writes to the potentially async-only output stream.
        using var ms = new MemoryStream();
        var outputStream = OutputStream.NotNull();
        var result = WriteHeader(
            ms,
            filename,
            zipWriterEntryOptions,
            entry,
            useZip64,
            outputStream.CanSeek,
            usesDataDescriptor
        );
        ms.Position = 0;
        await ms.CopyToAsync(outputStream, 81920, cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Writes a directory entry to the ZIP archive.
    /// </summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
#if !SYNC_ONLY
        cancellationToken.ThrowIfCancellationRequested();
#endif

        var normalizedName = NormalizeDirectoryName(directoryName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return;
        }

        var options = new ZipWriterEntryOptions { ModificationDateTime = modificationTime };
        await WriteDirectoryEntryAsync(normalizedName, options, cancellationToken)
            .ConfigureAwait(false);
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    private async ValueTask WriteDirectoryEntryAsync(
        string directoryPath,
        ZipWriterEntryOptions options,
        CancellationToken cancellationToken
    )
    {
        var compression = ZipCompressionMethod.None;

        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;

        var entry = new ZipCentralDirectoryEntry(
            compression,
            directoryPath,
            (ulong)streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime,
            Crc = 0,
            Compressed = 0,
            Decompressed = 0,
        };

        var useZip64 = isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        var headersize = (uint)
            await WriteHeaderAsync(
                    directoryPath,
                    options,
                    entry,
                    useZip64,
                    usesDataDescriptor: false,
                    cancellationToken
                )
                .ConfigureAwait(false);
        streamPosition += headersize;
        entries.Add(entry);
    }
}
