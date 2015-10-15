namespace SharpCompress.Common.Rar
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class RarCryptoWrapper : Stream
    {
        [CompilerGenerated]
        private long <Position>k__BackingField;
        private readonly Stream actualStream;
        private readonly Queue<byte> data = new Queue<byte>();
        private RarRijndael rijndael;
        private readonly byte[] salt;

        public RarCryptoWrapper(Stream actualStream, string password, byte[] salt)
        {
            this.actualStream = actualStream;
            this.salt = salt;
            this.rijndael = RarRijndael.InitializeFrom(password, salt);
        }

        protected override void Dispose(bool disposing)
        {
            if (this.rijndael != null)
            {
                this.rijndael.Dispose();
                this.rijndael = null;
            }
            base.Dispose(disposing);
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.salt == null)
            {
                return this.actualStream.Read(buffer, offset, count);
            }
            return this.ReadAndDecrypt(buffer, offset, count);
        }

        public int ReadAndDecrypt(byte[] buffer, int offset, int count)
        {
            int num = this.data.Count;
            int num2 = count - num;
            if (num2 > 0)
            {
                int num4;
                int num3 = num2 + ((~num2 + 1) & 15);
                for (num4 = 0; num4 < (num3 / 0x10); num4++)
                {
                    byte[] buffer2 = new byte[0x10];
                    this.actualStream.Read(buffer2, 0, 0x10);
                    byte[] buffer3 = this.rijndael.ProcessBlock(buffer2);
                    foreach (byte num5 in buffer3)
                    {
                        this.data.Enqueue(num5);
                    }
                }
                for (num4 = 0; num4 < count; num4++)
                {
                    buffer[offset + num4] = this.data.Dequeue();
                }
            }
            return count;
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

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            [CompilerGenerated]
            get
            {
                return this.<Position>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Position>k__BackingField = value;
            }
        }
    }
}

