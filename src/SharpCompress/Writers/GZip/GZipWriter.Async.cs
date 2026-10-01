using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;

namespace SharpCompress.Writers.GZip;

public partial class GZipWriter
{
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        if (_wroteToStream)
        {
            throw new ArgumentException("Can only write a single stream to a GZip file.");
        }

        // Custom providers need not expose SharpCompress's internal GZip stream.
        if (OutputStream is GZipStream gzipStream)
        {
            gzipStream.FileName = filename;
            gzipStream.LastModified = modificationTime;
        }

        var progressStream = WrapWithProgress(source, filename);
#if LEGACY_DOTNET
        await progressStream
            .CopyToAsync(OutputStream.NotNull(), WriterOptions.BufferSize)
            .ConfigureAwait(false);
#else
        await progressStream
            .CopyToAsync(OutputStream.NotNull(), WriterOptions.BufferSize, cancellationToken)
            .ConfigureAwait(false);
#endif
        _wroteToStream = true;
    }

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException("GZip archives do not support directory entries.");
}
