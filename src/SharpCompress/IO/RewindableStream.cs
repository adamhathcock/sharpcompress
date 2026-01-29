using System;
using System.Buffers;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.IO
{
    internal partial class RewindableStream(Stream stream) : Stream
    {
        private readonly int _bufferSize = Constants.RewindableBufferSize;
        private byte[]? _buffer = ArrayPool<byte>.Shared.Rent(Constants.RewindableBufferSize);
        private int _bufferLength = 0;
        private int _bufferPosition = 0;
        private bool _isRewound;
        private bool _isDisposed;

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
                ArrayPool<byte>.Shared.Return(_buffer!);
                _buffer = null;
                stream.Dispose();
            }
        }

        public void Rewind(bool stopRecording = false)
        {
            _isRewound = true;
            IsRecording = !stopRecording;
            _bufferPosition = 0;
        }

        public void Rewind(MemoryStream buffer)
        {
            long bufferLength = buffer.Length;
            if (_bufferPosition >= bufferLength)
            {
                _bufferPosition -= (int)bufferLength;
            }
            else
            {
                int bytesToKeep = _bufferLength - _bufferPosition;
                if (bytesToKeep > 0)
                {
                    Array.Copy(_buffer!, _bufferPosition, _buffer!, 0, bytesToKeep);
                }
                if (bufferLength > _bufferSize)
                {
                    throw new InvalidOperationException(
                        $"External buffer size ({bufferLength} bytes) exceeds internal buffer capacity ({_bufferSize} bytes)"
                    );
                }
                _bufferLength = (int)bufferLength;
                _bufferPosition = 0;
                buffer.Position = 0;
                int bytesRead = buffer.Read(_buffer!, 0, _bufferLength);
                _bufferLength = bytesRead;
                _bufferPosition = 0;
            }
            _isRewound = true;
        }

        public void StartRecording()
        {
            if (_bufferPosition != 0)
            {
                int bytesToKeep = _bufferLength - _bufferPosition;
                if (bytesToKeep > 0)
                {
                    Array.Copy(_buffer!, _bufferPosition, _buffer!, 0, bytesToKeep);
                }
                _bufferLength = bytesToKeep;
                _bufferPosition = 0;
            }
            IsRecording = true;
        }

        public void StopRecording()
        {
            _isRewound = true;
            IsRecording = false;
            _bufferPosition = 0;
        }

        public override bool CanRead => true;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => false;

        public override void Flush() => throw new NotSupportedException();

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get
            {
                if (_isRewound || _bufferPosition < _bufferLength)
                {
                    return stream.Position + _bufferPosition - _bufferLength;
                }
                return stream.Position;
            }
            set
            {
                if (!_isRewound)
                {
                    stream.Position = value;
                }
                else if (value < stream.Position - _bufferLength || value >= stream.Position)
                {
                    stream.Position = value;
                    _isRewound = false;
                    _bufferLength = 0;
                    _bufferPosition = 0;
                }
                else
                {
                    _bufferPosition = (int)(value - stream.Position + _bufferLength);
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return 0;
            }
            int read;
            if (_isRewound && _bufferPosition != _bufferLength)
            {
                read = ReadFromBuffer(buffer, offset, count);
                if (read < count)
                {
                    int tempRead = stream.Read(buffer, offset + read, count - read);
                    if (IsRecording)
                    {
                        WriteToBuffer(buffer, offset + read, tempRead);
                    }
                    read += tempRead;
                }
                if (_bufferPosition == _bufferLength)
                {
                    _isRewound = false;
                    _bufferPosition = 0;
                    if (!IsRecording)
                    {
                        _bufferLength = 0;
                    }
                }
                return read;
            }

            read = stream.Read(buffer, offset, count);
            if (IsRecording)
            {
                WriteToBuffer(buffer, offset, read);
                _bufferPosition = _bufferLength;
            }
            return read;
        }

#if !LEGACY_DOTNET
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }
            int read;
            if (_isRewound && _bufferPosition != _bufferLength)
            {
                read = ReadFromBuffer(buffer);
                if (read < buffer.Length)
                {
                    int tempRead = stream.Read(buffer.Slice(read));
                    if (IsRecording)
                    {
                        WriteToBuffer(buffer.Slice(read, tempRead));
                    }
                    read += tempRead;
                }
                if (_bufferPosition == _bufferLength)
                {
                    _isRewound = false;
                    _bufferPosition = 0;
                    if (!IsRecording)
                    {
                        _bufferLength = 0;
                    }
                }
                return read;
            }

            read = stream.Read(buffer);
            if (IsRecording)
            {
                WriteToBuffer(buffer.Slice(0, read));
                _bufferPosition = _bufferLength;
            }
            return read;
        }
#endif

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private int ReadFromBuffer(byte[] buffer, int offset, int count)
        {
            int bytesToRead = Math.Min(count, _bufferLength - _bufferPosition);
            if (bytesToRead > 0)
            {
                Array.Copy(_buffer!, _bufferPosition, buffer, offset, bytesToRead);
                _bufferPosition += bytesToRead;
            }
            return bytesToRead;
        }

        private int ReadFromBuffer(Span<byte> buffer)
        {
            int bytesToRead = Math.Min(buffer.Length, _bufferLength - _bufferPosition);
            if (bytesToRead > 0)
            {
                _buffer!.AsSpan(_bufferPosition, bytesToRead).CopyTo(buffer);
                _bufferPosition += bytesToRead;
            }
            return bytesToRead;
        }

        private void WriteToBuffer(byte[] data, int offset, int count)
        {
            int spaceAvailable = _bufferSize - _bufferLength;
            if (count > spaceAvailable)
            {
                throw new InvalidOperationException(
                    $"Buffer overflow: Cannot write {count} bytes. Only {spaceAvailable} bytes available in {_bufferSize} byte buffer."
                );
            }
            Array.Copy(data, offset, _buffer!, _bufferLength, count);
            _bufferLength += count;
        }

        private void WriteToBuffer(ReadOnlySpan<byte> data)
        {
            int spaceAvailable = _bufferSize - _bufferLength;
            if (data.Length > spaceAvailable)
            {
                throw new InvalidOperationException(
                    $"Buffer overflow: Cannot write {data.Length} bytes. Only {spaceAvailable} bytes available in {_bufferSize} byte buffer."
                );
            }
            data.CopyTo(_buffer!.AsSpan(_bufferLength));
            _bufferLength += data.Length;
        }
    }
}
