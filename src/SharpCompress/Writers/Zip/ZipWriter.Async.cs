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
        using var output = WriteToStream(entryPath, zipWriterEntryOptions);
        var progressStream = WrapWithProgress(source, entryPath);
        await progressStream.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(false);
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
