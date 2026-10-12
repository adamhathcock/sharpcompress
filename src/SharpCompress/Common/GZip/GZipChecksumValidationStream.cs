using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Crypto;

namespace SharpCompress.Common.GZip;

internal sealed class GZipChecksumValidationStream : Stream
{
    private readonly Stream _source;
    private readonly Stream _rawStream;
    private readonly string _entryName;
    private readonly uint? _expectedCrc;
    private readonly uint? _expectedSize;
    private readonly uint[] _crc32Table;
    private uint _seed = Crc32Stream.DEFAULT_SEED;
    private uint _size;
    private bool _validated;
    private readonly Func<Stream?>? _bufferedInput;

    internal GZipChecksumValidationStream(
        Stream source,
        Stream rawStream,
        string? entryName,
        uint? expectedCrc,
        uint? expectedSize,
        Func<Stream?>? bufferedInput = null
    )
    {
        _source = source;
        _rawStream = rawStream;
        _entryName = string.IsNullOrEmpty(entryName) ? "Entry" : entryName!;
        _expectedCrc = expectedCrc;
        _expectedSize = expectedSize;
        _bufferedInput = bufferedInput;
        _crc32Table = Crc32Stream.InitializeTable(Crc32Stream.DEFAULT_POLYNOMIAL);
    }

    public override bool CanRead => _source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _source.Length;

    public override long Position
    {
        get => _source.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _source.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _source.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (count != 0)
        {
            UpdateAndValidateAtEof(buffer.AsSpan(offset, read), read);
        }
        return read;
    }

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer)
    {
        var read = _source.Read(buffer);
        if (!buffer.IsEmpty)
        {
            UpdateAndValidateAtEof(buffer[..read], read);
        }
        return read;
    }
#endif

    public override int ReadByte()
    {
        var value = _source.ReadByte();
        if (value == -1)
        {
            Validate();
        }
        else
        {
            _seed = Crc32Stream.CalculateCrc(_crc32Table, _seed, (byte)value);
            _size++;
        }

        return value;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var read = await _source
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (count != 0)
        {
            if (read == 0)
            {
                await ValidateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                UpdateAndValidateAtEof(buffer.AsSpan(offset, read), read);
            }
        }
        return read;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var read = await _source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (!buffer.IsEmpty)
        {
            if (read == 0)
            {
                await ValidateAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                UpdateAndValidateAtEof(buffer.Span[..read], read);
            }
        }
        return read;
    }
#endif

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private void UpdateAndValidateAtEof(ReadOnlySpan<byte> buffer, int read)
    {
        if (read > 0)
        {
            _seed = Crc32Stream.CalculateCrc(_crc32Table, _seed, buffer);
            _size += unchecked((uint)read);
            return;
        }

        Validate();
    }

    private void Validate()
    {
        if (_validated)
        {
            return;
        }

        var expectedCrc = _expectedCrc;
        var expectedSize = _expectedSize;
        if (!expectedCrc.HasValue || !expectedSize.HasValue)
        {
            Span<byte> trailer = stackalloc byte[8];
            var bufferedCount = ReadBufferedTrailer(trailer);
            if (!_rawStream.ReadFully(trailer.Slice(bufferedCount)))
            {
                throw new IncompleteArchiveException("Unexpected end of GZIP trailer.");
            }
            expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
            expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer[4..]);
        }

        ValidateValues(expectedCrc.Value, expectedSize.Value);
    }

    private async ValueTask ValidateAsync(CancellationToken cancellationToken)
    {
        if (_validated)
        {
            return;
        }
        var expectedCrc = _expectedCrc;
        var expectedSize = _expectedSize;
        if (!expectedCrc.HasValue || !expectedSize.HasValue)
        {
            var trailer = new byte[8];
            var bufferedCount = ReadBufferedTrailer(trailer);
            if (
                !await _rawStream
                    .ReadFullyAsync(
                        trailer,
                        bufferedCount,
                        trailer.Length - bufferedCount,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                throw new IncompleteArchiveException("Unexpected end of GZIP trailer.");
            }
            expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
            expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer.AsSpan(4));
        }
        ValidateValues(expectedCrc.Value, expectedSize.Value);
    }

    private int ReadBufferedTrailer(Span<byte> trailer)
    {
        // The inflater may have read ahead beyond the final compressed bit.
        using var buffered = _bufferedInput?.Invoke();
        return buffered?.Read(trailer) ?? 0;
    }

    private void ValidateValues(uint expectedCrc, uint expectedSize)
    {
        var actualCrc = ~_seed;
        if (actualCrc != expectedCrc)
        {
            throw new InvalidFormatException(
                $"CRC mismatch for entry '{_entryName}'. Expected 0x{expectedCrc:X8}, actual 0x{actualCrc:X8}."
            );
        }

        if (_size != expectedSize)
        {
            throw new InvalidFormatException(
                $"Size mismatch for entry '{_entryName}'. Expected {expectedSize}, actual {_size}."
            );
        }
        _validated = true;
    }
}
