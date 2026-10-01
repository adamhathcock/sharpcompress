using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Crypto;

namespace SharpCompress.Common.GZip;

internal sealed partial class GZipChecksumValidationStream : Stream
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

    internal GZipChecksumValidationStream(
        Stream source,
        Stream rawStream,
        string? entryName,
        uint? expectedCrc,
        uint? expectedSize
    )
    {
        _source = source;
        _rawStream = rawStream;
        _entryName = string.IsNullOrEmpty(entryName) ? "Entry" : entryName!;
        _expectedCrc = expectedCrc;
        _expectedSize = expectedSize;
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

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _source.FlushAsync(cancellationToken);

    [Zomp.SyncMethodGenerator.CreateSyncVersion]
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
        UpdateAndValidateAtEof(buffer.AsSpan(offset, read), read);
        return read;
    }

#if !LEGACY_DOTNET
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        var read = await _source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
#if SYNC_ONLY
        UpdateAndValidateAtEof(buffer[..read], read);
#else
        UpdateAndValidateAtEof(buffer.Span[..read], read);
#endif
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

        _validated = true;

        var expectedCrc = _expectedCrc;
        var expectedSize = _expectedSize;
        if (!expectedCrc.HasValue || !expectedSize.HasValue)
        {
            Span<byte> trailer = stackalloc byte[8];
            _rawStream.ReadFully(trailer);
            expectedCrc = BinaryPrimitives.ReadUInt32LittleEndian(trailer);
            expectedSize = BinaryPrimitives.ReadUInt32LittleEndian(trailer[4..]);
        }

        var actualCrc = ~_seed;
        if (actualCrc != expectedCrc.Value)
        {
            throw new InvalidFormatException(
                $"CRC mismatch for entry '{_entryName}'. Expected 0x{expectedCrc.Value:X8}, actual 0x{actualCrc:X8}."
            );
        }

        if (_size != expectedSize.Value)
        {
            throw new InvalidFormatException(
                $"Size mismatch for entry '{_entryName}'. Expected {expectedSize.Value}, actual {_size}."
            );
        }
    }
}
