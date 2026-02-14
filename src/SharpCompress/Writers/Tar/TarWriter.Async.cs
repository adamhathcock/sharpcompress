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
    /// </summary>
    public override async ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        var normalizedName = NormalizeDirectoryName(directoryName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return;
        }

        var header = new TarHeader(WriterOptions.ArchiveEncoding);
        header.LastModifiedTime = modificationTime ?? TarHeader.EPOCH;
        header.Name = normalizedName;
        header.Size = 0;
        header.EntryType = EntryType.Directory;
        await header.WriteAsync(OutputStream.NotNull(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously writes a file entry to the TAR archive.
    /// </summary>
    public override async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    ) =>
        await WriteAsync(filename, source, modificationTime, null, cancellationToken)
            .ConfigureAwait(false);

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
        await header.WriteAsync(OutputStream.NotNull(), cancellationToken).ConfigureAwait(false);
        var progressStream = WrapWithProgress(source, filename);
        var written = await progressStream
            .TransferToAsync(OutputStream.NotNull(), realSize, cancellationToken)
            .ConfigureAwait(false);
        await PadTo512Async(written, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask PadTo512Async(long size, CancellationToken cancellationToken = default)
    {
        var zeros = unchecked((int)(((size + 511L) & ~511L) - size));
        if (zeros > 0)
        {
            await OutputStream
                .NotNull()
                .WriteAsync(new byte[zeros], 0, zeros, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
