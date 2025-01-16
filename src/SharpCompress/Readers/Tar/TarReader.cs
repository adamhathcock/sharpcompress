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
        stream.CheckNotNull(nameof(stream));
        options = options ?? new ReaderOptions();
        var rewindableStream = new RewindableStream(stream);
        rewindableStream.StartRecording();
        if (GZipArchive.IsGZipFile(rewindableStream))
        {
            rewindableStream.Rewind(false);
            var testStream = new GZipStream(rewindableStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                return new TarReader(rewindableStream, options, CompressionType.GZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }

        rewindableStream.Rewind(false);
        if (BZip2Stream.IsBZip2(rewindableStream))
        {
            rewindableStream.Rewind(false);
            var testStream = new BZip2Stream(rewindableStream, CompressionMode.Decompress, false);
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                return new TarReader(rewindableStream, options, CompressionType.BZip2);
            }
            throw new InvalidFormatException("Not a tar file.");
        }

        rewindableStream.Rewind(false);
        if (LZipStream.IsLZipFile(rewindableStream))
        {
            rewindableStream.Rewind(false);
            var testStream = new LZipStream(rewindableStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                return new TarReader(rewindableStream, options, CompressionType.LZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        rewindableStream.Rewind(true);
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
