using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.IO;

namespace SharpCompress.Writers.Tar;

public class TarWriter : AbstractWriter
{
    private readonly bool finalizeArchiveOnClose;

    public TarWriter(Stream destination, TarWriterOptions options)
        : base(ArchiveType.Tar, options)
    {
        finalizeArchiveOnClose = options.FinalizeArchiveOnClose;

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Tars require writable streams.");
        }
        if (WriterOptions.LeaveStreamOpen)
        {
            destination = NonDisposingStream.Create(destination);
        }
        switch (options.CompressionType)
        {
            case CompressionType.None:
                break;
            case CompressionType.BZip2:
                {
                    destination = new BZip2Stream(destination, CompressionMode.Compress, false);
                }
                break;
            case CompressionType.GZip:
                {
                    destination = new GZipStream(destination, CompressionMode.Compress);
                }
                break;
            case CompressionType.LZip:
                {
                    destination = new LZipStream(destination, CompressionMode.Compress);
                }
                break;
            default:
            {
                throw new InvalidFormatException(
                    "Tar does not support compression: " + options.CompressionType
                );
            }
        }
        InitializeStream(destination);
    }

    public override void Write(string filename, Stream source, DateTime? modificationTime) =>
        Write(filename, source, modificationTime, null);

    private string NormalizeFilename(string filename)
    {
        filename = filename.Replace('\\', '/');

        var pos = filename.IndexOf(':');
        if (pos >= 0)
        {
            filename = filename.Remove(0, pos + 1);
        }

        return filename.Trim('/');
    }

    public void Write(string filename, Stream source, DateTime? modificationTime, long? size)
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
        size = source.TransferTo(OutputStream, realSize);
        PadTo512(size.Value);
    }

    private void PadTo512(long size)
    {
        var zeros = unchecked((int)(((size + 511L) & ~511L) - size));

        OutputStream.Write(stackalloc byte[zeros]);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (finalizeArchiveOnClose)
            {
                OutputStream.Write(stackalloc byte[1024]);
            }
            switch (OutputStream)
            {
                case BZip2Stream b:
                {
                    b.Finish();
                    break;
                }
                case LZipStream l:
                {
                    l.Finish();
                    break;
                }
            }
        }
        base.Dispose(isDisposing);
    }
}
