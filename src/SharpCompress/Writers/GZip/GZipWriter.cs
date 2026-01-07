using System;
using System.IO;
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
            destination = SharpCompressStream.Create(destination, leaveOpen: true);
        }
        InitializeStream(
            new GZipStream(
                destination,
                CompressionMode.Compress,
                (CompressionLevel)(options?.CompressionLevel ?? (int)CompressionLevel.Default),
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
        var progressStream = WrapWithProgress(source, filename);
        progressStream.CopyTo(stream);
        _wroteToStream = true;
    }

    public override void WriteDirectory(string directoryName, DateTime? modificationTime) =>
        throw new NotSupportedException("GZip archives do not support directory entries.");
}
