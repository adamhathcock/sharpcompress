using System;
using System.IO;

namespace SharpCompress.Compressors.Xz.Filters;

internal sealed partial class BcjFilterStream : ReadOnlyStream
{
    private readonly byte[] _buffer = new byte[8192];
    private readonly Func<byte[], int, uint, int> _convert;
    private int _size;
    private int _filtered;
    private int _position;
    private uint _ip;
    private bool _endReached;

    internal BcjFilterStream(Stream stream, Func<byte[], int, uint, int> convert)
    {
        BaseStream = stream;
        _convert = convert;
    }

    private int CopyBuffered(Span<byte> destination)
    {
        var count = Math.Min(destination.Length, _filtered - _position);
        _buffer.AsSpan(_position, count).CopyTo(destination);
        _position += count;
        return count;
    }

    private void KeepUnfilteredTail()
    {
        // Only bytes reported as processed may be returned or advance the instruction pointer.
        // Keep incomplete instructions for the next input chunk, independently of caller reads.
        _size -= _filtered;
        Buffer.BlockCopy(_buffer, _filtered, _buffer, 0, _size);
        _position = 0;
        _filtered = 0;
    }

    private void ConvertBuffered(int bytesRead)
    {
        _size += bytesRead;
        _filtered = _convert(_buffer, _size, _ip);
        _ip = unchecked(_ip + (uint)_filtered);
        if (bytesRead == 0)
        {
            // An incomplete instruction at true EOF is literal data, not an error.
            _endReached = true;
            _filtered = _size;
        }
    }
}
