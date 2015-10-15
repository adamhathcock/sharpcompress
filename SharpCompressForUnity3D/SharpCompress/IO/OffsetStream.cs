namespace SharpCompress.IO
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;

    public class OffsetStream : Stream
    {
        private long _of_length;
        private Stream _originalStream;
        private long _os_originalCurrentPos;
        private long _os_originalOffset;
        [CompilerGenerated]
        private bool _KeepStreamsOpen_k__BackingField;

        public OffsetStream(Stream originalStream, long offset) : this(originalStream, offset, originalStream.Length - offset)
        {
        }

        public OffsetStream(Stream originalStream, long offset, long length)
        {
            if (originalStream == null)
            {
                throw new Exception("null source stream for new OffsetStream()");
            }
            this._originalStream = originalStream;
            this._os_originalOffset = offset;
            this._of_length = length;
            this.Position = 0L;
            Debug.Assert((length >= 0L) && (length <= (originalStream.Length - offset)));
            if ((length < 0L) || (length > (originalStream.Length - offset)))
            {
                throw new Exception("out length with offsetStream.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!(!disposing || this.KeepStreamsOpen))
            {
                this._originalStream.Dispose();
            }
        }

        public override void Flush()
        {
            this._originalStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this._originalStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long num = this._originalStream.Position + offset;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    num = this._os_originalOffset + offset;
                    break;

                case SeekOrigin.Current:
                    break;

                case SeekOrigin.End:
                    if (offset > 0L)
                    {
                        throw new Exception("OffsetStream.Seek(offset,SeekOrigin.End) outside offset stream limits");
                    }
                    num = (this._os_originalOffset + this._of_length) + offset;
                    break;

                default:
                    throw new Exception("unknown SeekOrigin value" + origin.ToString());
            }
            if ((num < this._os_originalOffset) || (num >= (this._os_originalOffset + this._of_length)))
            {
                throw new Exception("OffsetStream.Seek() outside offset stream limits" + string.Format(", parms({0},{1})", offset, origin.ToString()) + string.Format(", new_offset({0}) os_offset({1}) os_length({2})", num, this._os_originalOffset, this._of_length));
            }
            return (this._originalStream.Seek(num, SeekOrigin.Begin) - this._os_originalOffset);
        }

        public override void SetLength(long value)
        {
            if ((value - this._os_originalOffset) < this._of_length)
            {
                throw new Exception("setlength not allowed on Less than  OffsetStream");
            }
            long num = value + this._os_originalOffset;
            this._originalStream.SetLength(num);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this._originalStream.Write(buffer, offset, count);
        }

        public override bool CanRead
        {
            get
            {
                return this._originalStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this._originalStream.CanSeek;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return this._originalStream.CanTimeout;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this._originalStream.CanWrite;
            }
        }

        public bool KeepStreamsOpen
        {
            [CompilerGenerated]
            get
            {
                return this._KeepStreamsOpen_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._KeepStreamsOpen_k__BackingField = value;
            }
        }

        public override long Length
        {
            get
            {
                long num = this._originalStream.Length - this._os_originalOffset;
                if (num < this._of_length)
                {
                    throw new Exception("the offset length not allowed on  Less than  origibal input stream length! ");
                }
                return num;
            }
        }

        public override long Position
        {
            get
            {
                this._os_originalCurrentPos = this._originalStream.Position;
                return (this._os_originalCurrentPos - this._os_originalOffset);
            }
            set
            {
                this._os_originalCurrentPos = this._os_originalOffset + value;
                this._originalStream.Position = this._os_originalCurrentPos;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                return this._originalStream.ReadTimeout;
            }
            set
            {
                this._originalStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return this._originalStream.WriteTimeout;
            }
            set
            {
                this._originalStream.WriteTimeout = value;
            }
        }
    }
}

