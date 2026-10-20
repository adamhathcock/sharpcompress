using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Rar;

/// <summary>
/// A RarArchiveVolume is a single rar file that may or may not be a split RarArchive.  A Rar Archive is one to many Rar Parts
/// </summary>
public abstract class RarVolume : Volume
{
    private const int MaxCommentSize = 16 * 1024 * 1024;

    private readonly RarHeaderFactory _headerFactory;
    internal Rar5KeyCache KeyCache => _headerFactory.KeyCache;
    private int _maxCompressionAlgorithm;

    internal RarVolume(StreamingMode mode, Stream stream, ReaderOptions options, int index)
        : base(stream, options, index) => _headerFactory = new RarHeaderFactory(mode, options);

    private ArchiveHeader? ArchiveHeader { get; set; }

    internal bool IsHeaderEncrypted => _headerFactory.IsEncrypted;

    private StreamingMode Mode => _headerFactory.StreamingMode;

    internal abstract IEnumerable<RarFilePart> ReadFileParts();

    internal abstract IAsyncEnumerable<RarFilePart> ReadFilePartsAsync();

    internal abstract RarFilePart CreateFilePart(MarkHeader markHeader, FileHeader fileHeader);

    internal IEnumerable<RarFilePart> GetVolumeFileParts()
    {
        MarkHeader? lastMarkHeader = null;
        foreach (var header in _headerFactory.ReadHeaders(Stream))
        {
            switch (header.HeaderType)
            {
                case HeaderType.Mark:
                    {
                        lastMarkHeader = (MarkHeader)header;
                    }
                    break;
                case HeaderType.Archive:
                    {
                        ArchiveHeader = (ArchiveHeader)header;
                    }
                    break;
                case HeaderType.File:
                    {
                        var fh = (FileHeader)header;
                        if (_maxCompressionAlgorithm < fh.CompressionAlgorithm)
                        {
                            _maxCompressionAlgorithm = fh.CompressionAlgorithm;
                        }

                        yield return CreateFilePart(lastMarkHeader!, fh);
                    }
                    break;
                case HeaderType.Service:
                    {
                        var fh = (FileHeader)header;
                        if (fh.FileName == "CMT" && fh.IsStored)
                        {
                            // Read the logical service size, not the potentially padded packed size.
                            var commentSize = GetCommentSize(fh);
                            var buffer = ArrayPool<byte>.Shared.Rent(commentSize);
                            try
                            {
                                using var packedStream = fh.PackedStream.NotNull();
                                if (!packedStream.ReadFully(buffer.AsSpan(0, commentSize)))
                                {
                                    // Never decode unread bytes from a pooled buffer.
                                    throw new EndOfStreamException();
                                }
                                Comment = DecodeComment(buffer, commentSize);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }
                    }
                    break;
            }
        }
    }

    internal async IAsyncEnumerable<RarFilePart> GetVolumeFilePartsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        MarkHeader? lastMarkHeader = null;
        await foreach (
            var header in _headerFactory
                .ReadHeadersAsync(Stream)
                .WithCancellation(cancellationToken)
        )
        {
            switch (header.HeaderType)
            {
                case HeaderType.Mark:
                    {
                        lastMarkHeader = (MarkHeader)header;
                    }
                    break;
                case HeaderType.Archive:
                    {
                        ArchiveHeader = (ArchiveHeader)header;
                    }
                    break;
                case HeaderType.File:
                    {
                        var fh = (FileHeader)header;
                        if (_maxCompressionAlgorithm < fh.CompressionAlgorithm)
                        {
                            _maxCompressionAlgorithm = fh.CompressionAlgorithm;
                        }

                        yield return CreateFilePart(lastMarkHeader!, fh);
                    }
                    break;
                case HeaderType.Service:
                    {
                        var fh = (FileHeader)header;
                        if (fh.FileName == "CMT" && fh.IsStored)
                        {
                            var commentSize = GetCommentSize(fh);
                            var buffer = ArrayPool<byte>.Shared.Rent(commentSize);
                            try
                            {
                                using var packedStream = fh.PackedStream.NotNull();
                                if (
                                    !await packedStream
                                        .ReadFullyAsync(buffer, 0, commentSize, cancellationToken)
                                        .ConfigureAwait(false)
                                )
                                {
                                    // Never decode unread bytes from a pooled buffer.
                                    throw new EndOfStreamException();
                                }
                                Comment = DecodeComment(buffer, commentSize);
                            }
                            finally
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }
                    }
                    break;
            }
        }
    }

    private static int GetCommentSize(FileHeader header)
    {
        // Match UnRAR's limit for in-memory service data before renting a buffer.
        // Unknown unpacked sizes are represented by long.MaxValue and rejected too.
        if (header.UncompressedSize < 0 || header.UncompressedSize > MaxCommentSize)
        {
            throw new InvalidFormatException("RAR archive comment exceeds the 16 MiB size limit.");
        }

        return (int)header.UncompressedSize;
    }

    private static string DecodeComment(byte[] buffer, int commentSize)
    {
        // Like UnRAR, stop at the first NUL. Encrypted comments can include
        // padding in their unpacked size as well as their packed size.
        // Limit decoding to the payload, since rented buffers can be larger.
        var terminator = Array.IndexOf(buffer, (byte)0, 0, commentSize);
        var length = terminator < 0 ? commentSize : terminator;
        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private void EnsureArchiveHeaderLoaded()
    {
        if (ArchiveHeader is null)
        {
            if (Mode == StreamingMode.Streaming)
            {
                throw new ArchiveOperationException(
                    "ArchiveHeader should never been null in a streaming read."
                );
            }

            // we only want to load the archive header to avoid overhead but have to do the nasty thing and reset the stream
            _ = GetVolumeFileParts().First();
            Stream.Position = 0;
        }
    }

    /// <summary>
    /// RarArchive is the first volume of a multi-part archive.
    /// Only Rar 3.0 format and higher
    /// </summary>
    public override bool IsFirstVolume
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            return ArchiveHeader?.IsFirstVolume ?? false;
        }
    }

    /// <summary>
    /// RarArchive is part of a multi-part archive.
    /// </summary>
    public override bool IsMultiVolume
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            return ArchiveHeader?.IsVolume ?? false;
        }
    }

    /// <summary>
    /// RarArchive is SOLID (this means the Archive saved bytes by reusing information which helps for archives containing many small files).
    /// Currently, SharpCompress cannot decompress SOLID archives.
    /// </summary>
    public bool IsSolidArchive
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            return ArchiveHeader?.IsSolid ?? false;
        }
    }

    public async ValueTask<bool> IsSolidArchiveAsync(CancellationToken cancellationToken = default)
    {
        await EnsureArchiveHeaderLoadedAsync(cancellationToken).ConfigureAwait(false);
        return ArchiveHeader?.IsSolid ?? false;
    }

    public int MinVersion
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            if (_maxCompressionAlgorithm >= 50)
            {
                return 5; //5-6
            }
            else if (_maxCompressionAlgorithm >= 29)
            {
                return 3; //3-4
            }
            else if (_maxCompressionAlgorithm >= 20)
            {
                return 2; //2
            }
            else
            {
                return 1;
            }
        }
    }

    public int MaxVersion
    {
        get
        {
            EnsureArchiveHeaderLoaded();
            if (_maxCompressionAlgorithm >= 50)
            {
                return 6; //5-6
            }
            else if (_maxCompressionAlgorithm >= 29)
            {
                return 4; //3-4
            }
            else if (_maxCompressionAlgorithm >= 20)
            {
                return 2; //2
            }
            else
            {
                return 1;
            }
        }
    }

    private async ValueTask EnsureArchiveHeaderLoadedAsync(CancellationToken cancellationToken)
    {
        if (ArchiveHeader is null)
        {
            if (Mode == StreamingMode.Streaming)
            {
                throw new ArchiveOperationException(
                    "ArchiveHeader should never been null in a streaming read."
                );
            }

            // we only want to load the archive header to avoid overhead but have to do the nasty thing and reset the stream
#pragma warning disable CA2016 // Forward token if available; polyfill FirstAsync has no token overload
            await GetVolumeFilePartsAsync(cancellationToken).FirstAsync().ConfigureAwait(false);
#pragma warning restore CA2016
            Stream.Position = 0;
        }
    }

    public virtual async ValueTask<int> MinVersionAsync(
        CancellationToken cancellationToken = default
    )
    {
        await EnsureArchiveHeaderLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (_maxCompressionAlgorithm >= 50)
        {
            return 5; //5-6
        }
        else if (_maxCompressionAlgorithm >= 29)
        {
            return 3; //3-4
        }
        else if (_maxCompressionAlgorithm >= 20)
        {
            return 2; //2
        }
        else
        {
            return 1;
        }
    }

    public virtual async ValueTask<int> MaxVersionAsync(
        CancellationToken cancellationToken = default
    )
    {
        await EnsureArchiveHeaderLoadedAsync(cancellationToken).ConfigureAwait(false);
        if (_maxCompressionAlgorithm >= 50)
        {
            return 6; //5-6
        }
        else if (_maxCompressionAlgorithm >= 29)
        {
            return 4; //3-4
        }
        else if (_maxCompressionAlgorithm >= 20)
        {
            return 2; //2
        }
        else
        {
            return 1;
        }
    }

    public string? Comment { get; internal set; }
}
