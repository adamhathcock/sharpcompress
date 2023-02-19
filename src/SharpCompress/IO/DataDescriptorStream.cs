using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SharpCompress.IO;

public class DataDescriptorStream : Stream
{
    private readonly Stream _stream;
    private long _start;
    private int _search_position;
    private bool _isDisposed;
    private bool _done;

    private static byte[] DataDescriptorMarker = new byte[] { 0x50, 0x4b, 0x07, 0x08 };
    private static long DataDescriptorSize = 24;

    public DataDescriptorStream(Stream stream)
    {
        _stream = stream;
        _start = _stream.Position;
        _search_position = 0;
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

    public override void Flush() => throw new NotSupportedException();

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position;
        set => _stream.Position = value;
    }

    private bool validate_data_descriptor(Stream stream, long size)
    {
        var br = new BinaryReader(stream);
        br.ReadUInt32();
        br.ReadUInt32(); // CRC32 can be checked if we calculate it
        var compressed_size = br.ReadUInt32();
        var uncompressed_size = br.ReadUInt32();
        var uncompressed_64bit = br.ReadInt64();

        stream.Position -= DataDescriptorSize;

        var test_64bit = ((long)uncompressed_size << 32) | compressed_size;

        if (test_64bit == size && test_64bit == uncompressed_64bit)
        {
            return true;
        }

        if (compressed_size == size && compressed_size == uncompressed_size)
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

        int read = _stream.Read(buffer, offset, count);

        for( int i = 0; i < read; i++)
        {
            if (buffer[offset + i] == DataDescriptorMarker[_search_position] )
            {
                _search_position++;

                if (_search_position == 4)
                {
                    _search_position = 0;

                    if ( read - i > DataDescriptorSize)
                    {
                        var check = new MemoryStream(buffer, offset + i - 3, (int)DataDescriptorSize);
                        _done = validate_data_descriptor(check, _stream.Position - read + i - 3 - _start);

                        if( _done )
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
                _search_position = 0;
            }
        }

        if(_search_position > 0)
        {
            read -= _search_position;
            _stream.Position -= _search_position;
            _search_position = 0;
        }

        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
