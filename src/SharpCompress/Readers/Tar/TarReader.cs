using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Common.Tar;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;

namespace SharpCompress.Readers.Tar;

public class TarReader : AbstractReader<TarEntry, TarVolume>
{
    private readonly CompressionType compressionType;

    internal TarReader(Stream stream, ReaderOptions options, CompressionType compressionType)
        : base(options, ArchiveType.Tar)
    {
        this.compressionType = compressionType;
        Volume = new TarVolume(stream, options);
    }

    public override TarVolume Volume { get; }

    protected override Stream RequestInitialStream()
    {
        var stream = base.RequestInitialStream();
        return compressionType switch
        {
            CompressionType.BZip2 => new BZip2Stream(stream, CompressionMode.Decompress, false),
            CompressionType.GZip => new GZipStream(stream, CompressionMode.Decompress),
            CompressionType.ZStandard => new ZStandardStream(stream),
            CompressionType.LZip => new LZipStream(stream, CompressionMode.Decompress),
            CompressionType.Xz => new XZStream(stream),
            CompressionType.Lzw => new LzwStream(stream),
            CompressionType.None => stream,
            _ => throw new NotSupportedException("Invalid compression type: " + compressionType),
        };
    }

    #region Open

    /// <summary>
    /// Opens a TarReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static TarReader Open(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        options = options ?? new ReaderOptions();
        var rewindableStream = new SharpCompressStream(stream);

        long pos = ((IStreamStack)rewindableStream).GetPosition();

        if (GZipArchive.IsGZipFile(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var testStream = new GZipStream(rewindableStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                return new TarReader(rewindableStream, options, CompressionType.GZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }

        ((IStreamStack)rewindableStream).StackSeek(pos);
        if (BZip2Stream.IsBZip2(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var testStream = new BZip2Stream(rewindableStream, CompressionMode.Decompress, false);
            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                return new TarReader(rewindableStream, options, CompressionType.BZip2);
            }
            throw new InvalidFormatException("Not a tar file.");
        }

        ((IStreamStack)rewindableStream).StackSeek(pos);
        if (ZStandardStream.IsZStandard(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var testStream = new ZStandardStream(rewindableStream);
            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                return new TarReader(rewindableStream, options, CompressionType.ZStandard);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        ((IStreamStack)rewindableStream).StackSeek(pos);
        if (LZipStream.IsLZipFile(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var testStream = new LZipStream(rewindableStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                return new TarReader(rewindableStream, options, CompressionType.LZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }

        ((IStreamStack)rewindableStream).StackSeek(pos);
        return new TarReader(rewindableStream, options, CompressionType.None);
    }

    #endregion Open

    protected override IEnumerable<TarEntry> GetEntries(Stream stream) =>
        TarEntry.GetEntries(
            StreamingMode.Streaming,
            stream,
            compressionType,
            Options.ArchiveEncoding
        );
}
