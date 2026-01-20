using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.GZip;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;

namespace SharpCompress.Factories;

public class TarWrapper(
    CompressionType type,
    Func<Stream, bool> canHandle,
    Func<Stream, CancellationToken, ValueTask<bool>> canHandleAsync,
    Func<Stream, Stream> createStream,
    IEnumerable<string> knownExtensions,
    bool wrapInSharpCompressStream = true
)
{
    public CompressionType CompressionType { get; } = type;
    public Func<Stream, bool> IsMatch { get; } = canHandle;
    public Func<Stream, CancellationToken, ValueTask<bool>> IsMatchAsync { get; } = canHandleAsync;
    public bool WrapInSharpCompressStream { get; } = wrapInSharpCompressStream;

    public Func<Stream, Stream> CreateStream { get; } = createStream;

    public IEnumerable<string> KnownExtensions { get; } = knownExtensions;

    // https://en.wikipedia.org/wiki/Tar_(computing)#Suffixes_for_compressed_files
    public static TarWrapper[] Wrappers { get; } =
    [
        new(
            CompressionType.None,
            (_) => true,
            (_, _) => new ValueTask<bool>(true),
            (stream) => stream,
            ["tar"],
            false
        ), // We always do a test for IsTarFile later
        new(
            CompressionType.BZip2,
            BZip2Stream.IsBZip2,
            BZip2Stream.IsBZip2Async,
            (stream) => BZip2Stream.Create(stream, CompressionMode.Decompress, false),
            ["tar.bz2", "tb2", "tbz", "tbz2", "tz2"]
        ),
        new(
            CompressionType.GZip,
            GZipArchive.IsGZipFile,
            GZipArchive.IsGZipFileAsync,
            (stream) => new GZipStream(stream, CompressionMode.Decompress),
            ["tar.gz", "taz", "tgz"]
        ),
        new(
            CompressionType.ZStandard,
            ZStandardStream.IsZStandard,
            ZStandardStream.IsZStandardAsync,
            (stream) => new ZStandardStream(stream),
            ["tar.zst", "tar.zstd", "tzst", "tzstd"]
        ),
        new(
            CompressionType.LZip,
            LZipStream.IsLZipFile,
            LZipStream.IsLZipFileAsync,
            (stream) => new LZipStream(stream, CompressionMode.Decompress),
            ["tar.lz"]
        ),
        new(
            CompressionType.Xz,
            XZStream.IsXZStream,
            XZStream.IsXZStreamAsync,
            (stream) => new XZStream(stream),
            ["tar.xz", "txz"],
            false
        ),
        new(
            CompressionType.Lzw,
            LzwStream.IsLzwStream,
            LzwStream.IsLzwStreamAsync,
            (stream) => new LzwStream(stream),
            ["tar.Z", "tZ", "taZ"],
            false
        ),
    ];
}
