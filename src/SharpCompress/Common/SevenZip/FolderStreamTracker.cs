using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Keeps track of how much of a solid folder has actually been consumed from its shared decoder.
/// </summary>
/// <remarks>
/// <para>
/// Every entry of a solid folder is decoded by the same forward-only stream, and that stream
/// cannot be rewound. The position therefore has to be tracked explicitly: it tells the caller
/// whether the next entry can be reached by skipping forward over the bytes that were not read
/// (the common case) or whether the folder has to be decoded again from its start.
/// </para>
/// <para>
/// The tracker also pins the decoder to a single read mode. LzmaStream refuses to mix synchronous
/// and asynchronous reads on the same instance, and the decoder is shared by every entry of the
/// folder, so the first read decides the mode used by all later reads. Reads that arrive in the
/// other mode are routed to the mode the decoder was established with instead of failing.
/// </para>
/// <para>
/// The tracker does not own the decoder: the ArchiveDatabase creates and disposes it. Disposing
/// the tracker therefore leaves the shared decoder alone.
/// </para>
/// </remarks>
internal sealed class FolderStreamTracker : Stream, IStreamStack
{
    Stream IStreamStack.BaseStream() => _stream;

    private readonly Stream _stream;
    private readonly byte[] _singleByte = new byte[1];
    private long _position;
    private bool _asyncMode;
    private bool _modeEstablished;

    public FolderStreamTracker(Stream stream) =>
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));

    /// <summary>
    /// The number of decompressed bytes consumed from the folder so far.
    /// </summary>
    public override long Position
    {
        get => _position;
        set
        {
            if (!_stream.CanSeek)
            {
                throw new NotSupportedException();
            }

            _stream.Position = value;
            _position = value;
        }
    }

    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => _stream.Length;

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        if (_modeEstablished && _asyncMode)
        {
            // The decoder is already used asynchronously, so it must not be read synchronously.
#pragma warning disable VSTHRD002 // The decoder cannot be read synchronously in this mode.
            return ReadAsyncCore(buffer, offset, count, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002
        }

        _modeEstablished = true;
        _asyncMode = false;
        var read = _stream.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    public override int ReadByte()
    {
        if (_modeEstablished && _asyncMode)
        {
            // The decoder is already used asynchronously, so it must not be read synchronously.
#pragma warning disable VSTHRD002 // The decoder cannot be read synchronously in this mode.
            var read = ReadAsyncCore(_singleByte, 0, 1, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
#pragma warning restore VSTHRD002
            return read == 0 ? -1 : _singleByte[0];
        }

        _modeEstablished = true;
        _asyncMode = false;
        var value = _stream.ReadByte();
        if (value != -1)
        {
            _position++;
        }
        return value;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count <= 0)
        {
            return Task.FromResult(0);
        }

        if (_modeEstablished && !_asyncMode)
        {
            // The decoder is already used synchronously, so it must not be read asynchronously.
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        _modeEstablished = true;
        _asyncMode = true;
        return ReadAsyncCore(buffer, offset, count, cancellationToken);
    }

    private async Task<int> ReadAsyncCore(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        var read = await _stream
            .ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var position = _stream.Seek(offset, origin);
        _position = position;
        return position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override void WriteByte(byte value) => throw new NotSupportedException();
}
