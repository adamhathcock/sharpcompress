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
    Func<Stream, CancellationToken, ValueTask<Stream>> createStreamAsync,
    IEnumerable<string> knownExtensions,
    bool wrapInSharpCompressStream = true,
    int? minimumRewindBufferSize = null
)
{
    public CompressionType CompressionType { get; } = type;
    public Func<Stream, bool> IsMatch { get; } = canHandle;
    public Func<Stream, CancellationToken, ValueTask<bool>> IsMatchAsync { get; } = canHandleAsync;
    public bool WrapInSharpCompressStream { get; } = wrapInSharpCompressStream;

    /// <summary>
    /// The minimum ring buffer size required to detect and probe this format.
    /// Format detection reads a decompressed block to check the tar header, so
    /// the ring buffer must be large enough to hold the compressed bytes consumed
    /// during that probe. Defaults to <see cref="Common.Constants.RewindableBufferSize"/>.
    /// </summary>
    public int MinimumRewindBufferSize { get; } =
        minimumRewindBufferSize ?? Common.Constants.RewindableBufferSize;

    public Func<Stream, Stream> CreateStream { get; } = createStream;
    public Func<Stream, CancellationToken, ValueTask<Stream>> CreateStreamAsync { get; } =
        createStreamAsync;

    public IEnumerable<string> KnownExtensions { get; } = knownExtensions;

    // https://en.wikipedia.org/wiki/Tar_(computing)#Suffixes_for_compressed_files
    public static TarWrapper[] Wrappers { get; } =
    [
        new(
            CompressionType.None,
            (_) => true,
            (_, _) => new ValueTask<bool>(true),
            (stream) => stream,
            (stream, _) => new ValueTask<Stream>(stream),
            ["tar"],
            false
        ), // We always do a test for IsTarFile later
        new(
            CompressionType.BZip2,
            BZip2Stream.IsBZip2,
            BZip2Stream.IsBZip2Async,
            (stream) => BZip2Stream.Create(stream, CompressionMode.Decompress, false),
            async (stream, _) =>
                await BZip2Stream
                    .CreateAsync(stream, CompressionMode.Decompress, false)
                    .ConfigureAwait(false),
            ["tar.bz2", "tb2", "tbz", "tbz2", "tz2"],
            // BZip2 decompresses in whole blocks; the compressed size of the first block
            // can be close to the uncompressed maximum (9 × 100 000 = 900 000 bytes).
            // The ring buffer must hold all compressed bytes read during format detection.
            minimumRewindBufferSize: BZip2Constants.baseBlockSize * 9
        ),
        new(
            CompressionType.GZip,
            GZipArchive.IsGZipFile,
            GZipArchive.IsGZipFileAsync,
            (stream) => new GZipStream(stream, CompressionMode.Decompress),
            (stream, _) =>
                new ValueTask<Stream>(new GZipStream(stream, CompressionMode.Decompress)),
            ["tar.gz", "taz", "tgz"]
        ),
        new(
            CompressionType.ZStandard,
            ZStandardStream.IsZStandard,
            ZStandardStream.IsZStandardAsync,
            (stream) => new ZStandardStream(stream),
            (stream, _) => new ValueTask<Stream>(new ZStandardStream(stream)),
            ["tar.zst", "tar.zstd", "tzst", "tzstd"],
            // ZStandard decompresses in blocks; the compressed size of the first block
            // can be up to ZSTD_BLOCKSIZE_MAX + ZSTD_blockHeaderSize = 131075 bytes.
            // The ring buffer must hold all compressed bytes read during format detection.
            minimumRewindBufferSize: ZstandardConstants.DStreamInSize
        ),
        new(
            CompressionType.LZip,
            LZipStream.IsLZipFile,
            LZipStream.IsLZipFileAsync,
            (stream) => LZipStream.Create(stream, CompressionMode.Decompress),
            async (stream, _) =>await LZipStream.CreateAsync(stream, CompressionMode.Decompress).ConfigureAwait(false),
                                                    ["tar.lz"]
        ),
        new(
            CompressionType.Xz,
            XZStream.IsXZStream,
            XZStream.IsXZStreamAsync,
            (stream) => new XZStream(stream),
            (stream, _) => new ValueTask<Stream>(new XZStream(stream)),
            ["tar.xz", "txz"],
            false
        ),
        new(
            CompressionType.Lzw,
            LzwStream.IsLzwStream,
            LzwStream.IsLzwStreamAsync,
            (stream) => new LzwStream(stream),
            (stream, _) => new ValueTask<Stream>(new LzwStream(stream)),
            ["tar.Z", "tZ", "taZ"],
            false
        ),
    ];

    /// <summary>
    /// The largest <see cref="MinimumRewindBufferSize"/> across all registered wrappers.
    /// Use this as the ring buffer size when creating a stream for Tar format detection so
    /// that the buffer is sized correctly at construction and never needs to be reallocated.
    /// </summary>
    public static int MaximumRewindBufferSize { get; } = GetMaximumRewindBufferSize();

    // Computed after Wrappers is initialised so the static initialisation order is safe.
    private static int GetMaximumRewindBufferSize()
    {
        var max = 0;
        foreach (var w in Wrappers)
        {
            if (w.MinimumRewindBufferSize > max)
            {
                max = w.MinimumRewindBufferSize;
            }
        }
        return max;
    }
}
