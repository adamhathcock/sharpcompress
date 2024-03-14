using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SharpCompress.IO;

public class DataDescriptorStream : Stream
{
    private readonly Stream _stream;
    private long _start;
    private int _searchPosition;
    private bool _isDisposed;
    private bool _done;

    private static byte[] _dataDescriptorMarker = new byte[] { 0x50, 0x4b, 0x07, 0x08 };
    private static long _dataDescriptorSize = 24;

    public DataDescriptorStream(Stream stream)
    {
        _stream = stream;
        _start = _stream.Position;
        _searchPosition = 0;
        _done = false;
    }

    internal bool IsRecording { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        base.Dispose(disposing);
        if (disposing)
        {
            _stream.Dispose();
        }
    }

    public override bool CanRead => true;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position - _start;
        set => _stream.Position = value;
    }

    private bool validate_data_descriptor(Stream stream, long size)
    {
        var br = new BinaryReader(stream);
        br.ReadUInt32();
        br.ReadUInt32(); // CRC32 can be checked if we calculate it
        var compressedSize = br.ReadUInt32();
        var uncompressedSize = br.ReadUInt32();
        var uncompressed64Bit = br.ReadInt64();

        stream.Position -= _dataDescriptorSize;

        var test64Bit = ((long)uncompressedSize << 32) | compressedSize;

        if (test64Bit == size && test64Bit == uncompressed64Bit)
        {
            return true;
        }

        if (compressedSize == size && compressedSize == uncompressedSize)
        {
            return true;
        }

        return false;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0 || _done)
        {
            return 0;
        }

        var read = _stream.Read(buffer, offset, count);

        for (var i = 0; i < read; i++)
        {
            if (buffer[offset + i] == _dataDescriptorMarker[_searchPosition])
            {
                _searchPosition++;

                if (_searchPosition == 4)
                {
                    _searchPosition = 0;

                    if (read - i > _dataDescriptorSize)
                    {
                        var check = new MemoryStream(
                            buffer,
                            offset + i - 3,
                            (int)_dataDescriptorSize
                        );
                        _done = validate_data_descriptor(
                            check,
                            _stream.Position - read + i - 3 - _start
                        );

                        if (_done)
                        {
                            _stream.Position = _stream.Position - read + i - 3;

                            return i - 3;
                        }
                    }
                    else
                    {
                        _stream.Position = _stream.Position - read + i - 3;

                        _done = validate_data_descriptor(_stream, _stream.Position - _start);

                        return i - 3;
                    }
                }
            }
            else
            {
                _searchPosition = 0;
            }
        }

        if (_searchPosition > 0)
        {
            read -= _searchPosition;
            _stream.Position -= _searchPosition;
            _searchPosition = 0;
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
