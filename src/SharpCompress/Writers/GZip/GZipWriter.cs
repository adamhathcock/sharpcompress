using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;

namespace SharpCompress.Writers.GZip;

public sealed partial class GZipWriter : AbstractWriter
{
    private bool _wroteToStream;

    public GZipWriter(Stream destination, GZipWriterOptions? options = null)
        : base(ArchiveType.GZip, options ?? new GZipWriterOptions())
    {
        if (WriterOptions.LeaveStreamOpen)
        {
            destination = SharpCompressStream.CreateNonDisposing(destination);
        }

        // Use the configured compression providers
        var providers = WriterOptions.Providers;

        // Create the GZip stream using the provider
        var compressionStream = providers.CreateCompressStream(
            CompressionType.GZip,
            destination,
            WriterOptions.CompressionLevel
        );

        // If using internal GZipStream, set the encoding for header filename
        if (compressionStream is GZipStream gzipStream)
        {
            // Note: FileName and LastModified will be set in Write()
        }

        InitializeStream(compressionStream);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            //dispose here to finish the GZip, GZip won't close the underlying stream
            OutputStream.NotNull().Dispose();
        }
        base.Dispose(isDisposing);
    }

    public override void Write(string filename, Stream source, DateTime? modificationTime)
    {
        if (_wroteToStream)
        {
            throw new ArgumentException("Can only write a single stream to a GZip file.");
        }

        // Set metadata on the stream if it's the internal GZipStream
        if (OutputStream is GZipStream gzipStream)
        {
            gzipStream.FileName = filename;
            gzipStream.LastModified = modificationTime;
        }

        var progressStream = WrapWithProgress(source, filename);
        progressStream.CopyTo(OutputStream.NotNull(), Constants.BufferSize);
        _wroteToStream = true;
    }

    public override void WriteDirectory(string directoryName, DateTime? modificationTime) =>
        throw new NotSupportedException("GZip archives do not support directory entries.");
}
