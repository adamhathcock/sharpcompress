using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;

namespace SharpCompress.Writers.GZip;

public sealed class GZipWriter : AbstractWriter
{
    private bool _wroteToStream;

    public GZipWriter(Stream destination, GZipWriterOptions? options = null)
        : base(ArchiveType.GZip, options ?? new GZipWriterOptions())
    {
        if (WriterOptions.LeaveStreamOpen)
        {
            destination = NonDisposingStream.Create(destination);
        }
        InitalizeStream(
            new GZipStream(
                destination,
                CompressionMode.Compress,
                options?.CompressionLevel ?? CompressionLevel.Default,
                WriterOptions.ArchiveEncoding.GetEncoding()
            )
        );
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            //dispose here to finish the GZip, GZip won't close the underlying stream
            OutputStream.Dispose();
        }
        base.Dispose(isDisposing);
    }

    public override void Write(string filename, Stream source, DateTime? modificationTime)
    {
        if (_wroteToStream)
        {
            throw new ArgumentException("Can only write a single stream to a GZip file.");
        }
        var stream = (GZipStream)OutputStream;
        stream.FileName = filename;
        stream.LastModified = modificationTime;
        source.TransferTo(stream);
        _wroteToStream = true;
    }

#if !NETFRAMEWORK && !NETSTANDARD2_0
    public override ValueTask DisposeAsync() => throw new NotImplementedException();

    public override ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
#endif
}
