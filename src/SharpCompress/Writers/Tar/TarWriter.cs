using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
    private TarHeaderWriteFormat headerFormat;

    public TarWriter(Stream destination, TarWriterOptions options)
        : base(ArchiveType.Tar, options)
    {
        finalizeArchiveOnClose = options.FinalizeArchiveOnClose;
        headerFormat = options.HeaderFormat;

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Tars require writable streams.");
        }
        if (WriterOptions.LeaveStreamOpen)
        {
            destination = SharpCompressStream.Create(destination, leaveOpen: true);
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

    private string NormalizeDirectoryName(string directoryName)
    {
        directoryName = NormalizeFilename(directoryName);
        // Ensure directory name ends with '/' for tar format
        if (!string.IsNullOrEmpty(directoryName) && !directoryName.EndsWith('/'))
        {
            directoryName += '/';
        }
        return directoryName;
    }

    public override void WriteDirectory(string directoryName, DateTime? modificationTime)
    {
        var normalizedName = NormalizeDirectoryName(directoryName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return; // Skip empty or root directory
        }

        var header = new TarHeader(WriterOptions.ArchiveEncoding);
        header.LastModifiedTime = modificationTime ?? TarHeader.EPOCH;
        header.Name = normalizedName;
        header.Size = 0;
        header.EntryType = EntryType.Directory;
        header.Write(OutputStream);
    }

    public override async Task WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        // Synchronous implementation is sufficient for header-only write
        WriteDirectory(directoryName, modificationTime);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public void Write(string filename, Stream source, DateTime? modificationTime, long? size)
    {
        if (!source.CanSeek && size is null)
        {
            throw new ArgumentException("Seekable stream is required if no size is given.");
        }

        var realSize = size ?? source.Length;

        var header = new TarHeader(WriterOptions.ArchiveEncoding, headerFormat);

        header.LastModifiedTime = modificationTime ?? TarHeader.EPOCH;
        header.Name = NormalizeFilename(filename);
        header.Size = realSize;
        header.Write(OutputStream);
        var progressStream = WrapWithProgress(source, filename);
        size = progressStream.TransferTo(OutputStream, realSize);
        PadTo512(size.Value);
    }

    public override async Task WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    ) => await WriteAsync(filename, source, modificationTime, null, cancellationToken);

    public async Task WriteAsync(
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
