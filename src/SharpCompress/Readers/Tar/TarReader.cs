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

public partial class TarReader : AbstractReader<TarEntry, TarVolume>
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
            CompressionType.BZip2 => BZip2Stream.Create(stream, CompressionMode.Decompress, false),
            CompressionType.GZip => new GZipStream(stream, CompressionMode.Decompress),
            CompressionType.ZStandard => new ZStandardStream(stream),
            CompressionType.LZip => new LZipStream(stream, CompressionMode.Decompress),
            CompressionType.Xz => new XZStream(stream),
            CompressionType.Lzw => new LzwStream(stream),
            CompressionType.None => stream,
            _ => throw new NotSupportedException("Invalid compression type: " + compressionType),
        };
    }

    #region OpenReader

    /// <summary>
    /// Opens a TarReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        options = options ?? new ReaderOptions();
        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize:options.RewindableBufferSize
        );
        long pos = sharpCompressStream.Position;
        if (GZipArchive.IsGZipFile(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = new GZipStream(sharpCompressStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.GZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        if (BZip2Stream.IsBZip2(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = BZip2Stream.Create(
                sharpCompressStream,
                CompressionMode.Decompress,
                false
            );
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.BZip2);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        if (ZStandardStream.IsZStandard(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = new ZStandardStream(sharpCompressStream);
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.ZStandard);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        if (LZipStream.IsLZipFile(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = new LZipStream(sharpCompressStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.LZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        return new TarReader(sharpCompressStream, options, CompressionType.None);
    }

    #endregion OpenReader

    protected override IEnumerable<TarEntry> GetEntries(Stream stream) =>
        TarEntry.GetEntries(
            StreamingMode.Streaming,
            stream,
            compressionType,
            Options.ArchiveEncoding
        );

    // GetEntriesAsync moved to TarReader.Async.cs
}
