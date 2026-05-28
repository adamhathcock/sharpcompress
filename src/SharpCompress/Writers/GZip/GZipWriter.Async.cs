using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Deflate;

namespace SharpCompress.Writers.GZip;

public partial class GZipWriter
{
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
        var stream = (GZipStream)OutputStream.NotNull();
        stream.FileName = filename;
        stream.LastModified = modificationTime;
        var progressStream = WrapWithProgress(source, filename);
#if LEGACY_DOTNET
        await progressStream.CopyToAsync(stream).ConfigureAwait(false);
#else
        await progressStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
#endif
        _wroteToStream = true;
    }

    public override ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException("GZip archives do not support directory entries.");
}
