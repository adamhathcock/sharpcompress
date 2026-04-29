using System;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Providers;

namespace SharpCompress.Writers.Tar;

public partial class TarWriter : AbstractWriter
{
    private readonly bool _finalizeArchiveOnClose;
    private readonly TarHeaderWriteFormat _headerFormat;

    public TarWriter(Stream destination, TarWriterOptions options)
        : base(ArchiveType.Tar, options)
    {
        _finalizeArchiveOnClose = options.FinalizeArchiveOnClose;
        _headerFormat = options.HeaderFormat;


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

        var header = new TarHeader(WriterOptions.ArchiveEncoding, _headerFormat);

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
        if (isDisposing && !_isDisposed)
        {
            if (_finalizeArchiveOnClose)
            {
                OutputStream.NotNull().Write(stackalloc byte[1024]);
            }
            // Use IFinishable interface for generic finalization
            if (OutputStream is IFinishable finishable)
            {
                finishable.Finish();
            }
            _isDisposed = true;
        }
        base.Dispose(isDisposing);
    }
}
