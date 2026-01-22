using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;

namespace SharpCompress.Writers.Tar;

public partial class TarWriter
{
    /// <summary>
    /// Asynchronously writes a directory entry to the TAR archive.
    /// Uses synchronous implementation for directory entries as they are lightweight.
    /// </summary>
    public override async ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        // Synchronous implementation is sufficient for header-only write
        WriteDirectory(directoryName, modificationTime);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes a file entry to the TAR archive.
    /// </summary>
    public override async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    ) => await WriteAsync(filename, source, modificationTime, null, cancellationToken);

    /// <summary>
    /// Asynchronously writes a file entry with optional size specification.
    /// </summary>
    public async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        long? size,
        CancellationToken cancellationToken = default
    )
    {
        if (!source.CanSeek && size is null)
        {
            throw new ArgumentException("Seekable stream is required if no size is given.");
        }

        var realSize = size ?? source.Length;

        var header = new TarHeader(WriterOptions.ArchiveEncoding);

        header.LastModifiedTime = modificationTime ?? TarHeader.EPOCH;
        header.Name = NormalizeFilename(filename);
        header.Size = realSize;
        header.Write(OutputStream);
        var progressStream = WrapWithProgress(source, filename);
        var written = await progressStream
            .TransferToAsync(OutputStream, realSize, cancellationToken)
            .ConfigureAwait(false);
        PadTo512(written);
    }
}
