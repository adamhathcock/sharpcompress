using System;
using System.IO;

namespace SharpCompress.IO
{
    internal partial class RewindableStream(Stream stream) : Stream
    {
        private MemoryStream _bufferStream = new MemoryStream();
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
                stream.Dispose();
            }
        }

        public void Rewind(bool stopRecording = false)
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
                _bufferStream.TransferTo(buffer, buffer.Length - _bufferStream.Position);
                //create new memorystream to allow proper resizing as memorystream could be a user provided buffer
                //https://github.com/adamhathcock/sharpcompress/issues/306
                _bufferStream = new MemoryStream();
                buffer.Position = 0;
                buffer.TransferTo(_bufferStream, buffer.Length);
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
                byte[] data = _bufferStream.ToArray();
                long position = _bufferStream.Position;
                _bufferStream.SetLength(0);
                _bufferStream.Write(data, (int)position, data.Length - (int)position);
                _bufferStream.Position = 0;
            }
            IsRecording = true;
        }

        public override bool CanRead => true;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => false;

        public override void Flush() => throw new NotSupportedException();

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => stream.Position + _bufferStream.Position - _bufferStream.Length;
            set
            {
                if (!_isRewound)
                {
                    stream.Position = value;
                }
                else if (value < stream.Position - _bufferStream.Length || value >= stream.Position)
                {
                    stream.Position = value;
                    _isRewound = false;
                    _bufferStream.SetLength(0);
                }
                else
                {
                    _bufferStream.Position = value - stream.Position + _bufferStream.Length;
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
                read = _bufferStream.Read(buffer, offset, count);
                if (read < count)
                {
                    int tempRead = stream.Read(buffer, offset + read, count - read);
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

            read = stream.Read(buffer, offset, count);
            if (IsRecording)
            {
                _bufferStream.Write(buffer, offset, read);
            }
            return read;
        }

#if !LEGACY_DOTNET
        public override int Read(Span<byte> buffer)
        {
            //don't actually read if we don't really want to read anything
            //currently a network stream bug on Windows for .NET Core
            if (buffer.Length == 0)
            {
                return 0;
            }
            int read;
            if (_isRewound && _bufferStream.Position != _bufferStream.Length)
            {
                read = _bufferStream.Read(buffer);
                if (read < buffer.Length)
                {
                    int tempRead = stream.Read(buffer.Slice(read));
                    if (IsRecording)
                    {
                        _bufferStream.Write(buffer.Slice(read, tempRead));
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

            read = stream.Read(buffer);
            if (IsRecording)
            {
                _bufferStream.Write(buffer.Slice(0, read));
            }
            return read;
        }
#endif

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
