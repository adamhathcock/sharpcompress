using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Writers.Tar;

public partial class TarWriter : AbstractWriter
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
            destination = SharpCompressStream.CreateNonDisposing(destination);
        }

        var providers = options.Providers;

        destination = options.CompressionType switch
        {
            CompressionType.None => destination,
            CompressionType.BZip2 => providers.CreateCompressStream(
                CompressionType.BZip2,
                destination,
                options.CompressionLevel
            ),
            CompressionType.GZip => providers.CreateCompressStream(
                CompressionType.GZip,
                destination,
                options.CompressionLevel
            ),
            CompressionType.LZip => providers.CreateCompressStream(
                CompressionType.LZip,
                destination,
                options.CompressionLevel
            ),
            _ => throw new InvalidFormatException(
                "Tar does not support compression: " + options.CompressionType
            ),
        };

        InitializeStream(destination);
    }

    public override void Write(string filename, Stream source, DateTime? modificationTime) =>
        Write(filename, source, modificationTime, null);

    private string NormalizeFilename(string filename)
    {
        filename = filename.Replace('\\', '/');

#if LEGACY_DOTNET
        var pos = filename.IndexOf(':');
#else
        var pos = filename.IndexOf(':', StringComparison.Ordinal);
#endif
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
        header.Write(OutputStream.NotNull());
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
        header.Write(OutputStream.NotNull());
        var progressStream = WrapWithProgress(source, filename);
        size = progressStream.TransferTo(OutputStream.NotNull(), realSize);
        PadTo512(size.Value);
    }

    private void PadTo512(long size)
    {
        var zeros = unchecked((int)(((size + 511L) & ~511L) - size));

        OutputStream.NotNull().Write(stackalloc byte[zeros]);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (finalizeArchiveOnClose)
            {
                OutputStream.NotNull().Write(stackalloc byte[1024]);
            }
            // Use IFinishable interface for generic finalization
            if (OutputStream is IFinishable finishable)
            {
                finishable.Finish();
            }
        }
        base.Dispose(isDisposing);
    }
}
