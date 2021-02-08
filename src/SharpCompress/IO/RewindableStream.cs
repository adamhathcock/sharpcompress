using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    internal class RewindableStream : Stream
    {
        private readonly Stream stream;
        private MemoryStream bufferStream = new();
        private bool isRewound;
        private bool isDisposed;

        public RewindableStream(Stream stream)
        {
            this.stream = stream;
        }

        internal bool IsRecording { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            base.Dispose(disposing);
            if (disposing)
            {
                stream.Dispose();
            }
        }

        public void Rewind(bool stopRecording)
        {
            isRewound = true;
            IsRecording = !stopRecording;
            bufferStream.Position = 0;
        }

        public void Rewind(MemoryStream buffer)
        {
            if (bufferStream.Position >= buffer.Length)
            {
                bufferStream.Position -= buffer.Length;
            }
            else
            {

                bufferStream.TransferTo(buffer);
                //create new memorystream to allow proper resizing as memorystream could be a user provided buffer
                //https://github.com/adamhathcock/sharpcompress/issues/306
                bufferStream = new MemoryStream();
                buffer.Position = 0;
                buffer.TransferTo(bufferStream);
                bufferStream.Position = 0;
            }
            isRewound = true;
        }

        public void StartRecording()
        {
            //if (isRewound && bufferStream.Position != 0)
            //   throw new System.NotImplementedException();
            if (bufferStream.Position != 0)
            {
                byte[] data = bufferStream.ToArray();
                long position = bufferStream.Position;
                bufferStream.SetLength(0);
                bufferStream.Write(data, (int)position, data.Length - (int)position);
                bufferStream.Position = 0;
            }
            IsRecording = true;
        }

        public override bool CanRead => true;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => false;

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position + bufferStream.Position - bufferStream.Length;
            set
            {
                if (!isRewound)
                {
                    stream.Position = value;
                }
                else if (value < stream.Position - bufferStream.Length || value >= stream.Position)
                {
                    stream.Position = value;
                    isRewound = false;
                    bufferStream.SetLength(0);
                }
                else
                {
                    bufferStream.Position = value - stream.Position + bufferStream.Length;
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //don't actually read if we don't really want to read anything
            //currently a network stream bug on Windows for .NET Core
            if (count == 0)
            {
                return 0;
            }
            int read;
            if (isRewound && bufferStream.Position != bufferStream.Length)
            {
                read = await bufferStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (read < count)
                {
                    int tempRead = await stream.ReadAsync(buffer, read, count - read, cancellationToken);
                    if (IsRecording)
                    {
                        await bufferStream.WriteAsync(buffer, read, tempRead, cancellationToken);
                    }
                    read += tempRead;
                }
                if (bufferStream.Position == bufferStream.Length && !IsRecording)
                {
                    isRewound = false;
                    bufferStream.SetLength(0);
                }
                return read;
            }

            read = await stream.ReadAsync(buffer, cancellationToken);
            if (IsRecording)
            {
                await bufferStream.WriteAsync(buffer, cancellationToken);
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = buffer.Length;
            //don't actually read if we don't really want to read anything
            //currently a network stream bug on Windows for .NET Core
            if (count == 0)
            {
                return 0;
            }
            int read;
            if (isRewound && bufferStream.Position != bufferStream.Length)
            {
                read = await bufferStream.ReadAsync(buffer, cancellationToken);
                if (read < count)
                {
                    int tempRead = await stream.ReadAsync(buffer.Slice(read, count - read), cancellationToken);
                    if (IsRecording)
                    {
                        await bufferStream.WriteAsync(buffer.Slice(read, tempRead), cancellationToken);
                    }
                    read += tempRead;
                }
                if (bufferStream.Position == bufferStream.Length && !IsRecording)
                {
                    isRewound = false;
                    bufferStream.SetLength(0);
                }
                return read;
            }

            read = await stream.ReadAsync(buffer, cancellationToken);
            if (IsRecording)
            {
                await bufferStream.WriteAsync(buffer, cancellationToken);
            }
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}