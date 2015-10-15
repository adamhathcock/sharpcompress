namespace SharpCompress.IO
{
    using SharpCompress;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class RewindableStream : Stream
    {
        [CompilerGenerated]
        private bool <IsRecording>k__BackingField;
        private MemoryStream bufferStream = new MemoryStream();
        private bool isDisposed;
        private bool isRewound;
        private readonly Stream stream;

        public RewindableStream(Stream stream)
        {
            this.stream = stream;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                base.Dispose(disposing);
                if (disposing)
                {
                    this.stream.Dispose();
                }
            }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int num;
            if (this.isRewound && (this.bufferStream.Position != this.bufferStream.Length))
            {
                num = this.bufferStream.Read(buffer, offset, count);
                if (num < count)
                {
                    int num2 = this.stream.Read(buffer, offset + num, count - num);
                    if (this.IsRecording)
                    {
                        this.bufferStream.Write(buffer, offset + num, num2);
                    }
                    num += num2;
                }
                if (!((this.bufferStream.Position != this.bufferStream.Length) || this.IsRecording))
                {
                    this.isRewound = false;
                    this.bufferStream.SetLength(0L);
                }
                return num;
            }
            num = this.stream.Read(buffer, offset, count);
            if (this.IsRecording)
            {
                this.bufferStream.Write(buffer, offset, num);
            }
            return num;
        }

        public void Rewind(bool stopRecording)
        {
            this.isRewound = true;
            this.IsRecording = !stopRecording;
            this.bufferStream.Position = 0L;
        }

        public void Rewind(MemoryStream buffer)
        {
            if (this.bufferStream.Position >= buffer.Length)
            {
                this.bufferStream.Position -= buffer.Length;
            }
            else
            {
                Utility.TransferTo(this.bufferStream, buffer);
                this.bufferStream = buffer;
                this.bufferStream.Position = 0L;
            }
            this.isRewound = true;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public void StartRecording()
        {
            if (this.bufferStream.Position != 0L)
            {
                byte[] buffer = this.bufferStream.ToArray();
                long position = this.bufferStream.Position;
                this.bufferStream.SetLength(0L);
                this.bufferStream.Write(buffer, (int) position, buffer.Length - ((int) position));
                this.bufferStream.Position = 0L;
            }
            this.IsRecording = true;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        internal bool IsRecording
        {
            [CompilerGenerated]
            get
            {
                return this.<IsRecording>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<IsRecording>k__BackingField = value;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                return ((this.stream.Position + this.bufferStream.Position) - this.bufferStream.Length);
            }
            set
            {
                if (!this.isRewound)
                {
                    this.stream.Position = value;
                }
                else if ((value < (this.stream.Position - this.bufferStream.Length)) || (value >= this.stream.Position))
                {
                    this.stream.Position = value;
                    this.isRewound = false;
                    this.bufferStream.SetLength(0L);
                }
                else
                {
                    this.bufferStream.Position = (value - this.stream.Position) + this.bufferStream.Length;
                }
            }
        }
    }
}

