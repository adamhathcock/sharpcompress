using System;
using System.IO;

namespace SharpCompress.IO;

public class RewindableStream : Stream
{
    private readonly Stream _stream;
    private MemoryStream _bufferStream = new();
    private bool _isRewound;
    private bool _isDisposed;

    public RewindableStream(Stream stream) => this._stream = stream;

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

    public void Rewind(bool stopRecording)
    {
        _isRewound = true;
        IsRecording = !stopRecording;
        _bufferStream.Position = 0;
    }

    public void Rewind(MemoryStream buffer)
    {
        if (_bufferStream.Position >= buffer.Length)
        {
            _bufferStream.Position -= buffer.Length;
        }
        else
        {
            _bufferStream.TransferTo(buffer);
            //create new memorystream to allow proper resizing as memorystream could be a user provided buffer
            //https://github.com/adamhathcock/sharpcompress/issues/306
            _bufferStream = new MemoryStream();
            buffer.Position = 0;
            buffer.TransferTo(_bufferStream);
            _bufferStream.Position = 0;
        }
        _isRewound = true;
    }

    public void StartRecording()
    {
        //if (isRewound && bufferStream.Position != 0)
        //   throw new System.NotImplementedException();
        if (_bufferStream.Position != 0)
        {
            var data = _bufferStream.ToArray();
            var position = _bufferStream.Position;
            _bufferStream.SetLength(0);
            _bufferStream.Write(data, (int)position, data.Length - (int)position);
            _bufferStream.Position = 0;
        }
        IsRecording = true;
    }

    public override bool CanRead => true;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => _stream.Length;

    public override long Position
    {
        get => _stream.Position + _bufferStream.Position - _bufferStream.Length;
        set
        {
            if (!_isRewound)
            {
                _stream.Position = value;
            }
            else if (value < _stream.Position - _bufferStream.Length || value >= _stream.Position)
            {
                _stream.Position = value;
                _isRewound = false;
                _bufferStream.SetLength(0);
            }
            else
            {
                _bufferStream.Position = value - _stream.Position + _bufferStream.Length;
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        //don't actually read if we don't really want to read anything
        //currently a network stream bug on Windows for .NET Core
        if (count == 0)
        {
            return 0;
        }
        int read;
        if (_isRewound && _bufferStream.Position != _bufferStream.Length)
        {
            // don't read more than left
            var readCount = Math.Min(count, (int)(_bufferStream.Length - _bufferStream.Position));
            read = _bufferStream.Read(buffer, offset, readCount);
            if (read < readCount)
            {
                var tempRead = _stream.Read(buffer, offset + read, count - read);
                if (IsRecording)
                {
                    _bufferStream.Write(buffer, offset + read, tempRead);
                }
                read += tempRead;
            }
            if (_bufferStream.Position == _bufferStream.Length && !IsRecording)
            {
                _isRewound = false;
                _bufferStream.SetLength(0);
            }
            return read;
        }

        read = _stream.Read(buffer, offset, count);
        if (IsRecording)
        {
            _bufferStream.Write(buffer, offset, read);
        }
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
