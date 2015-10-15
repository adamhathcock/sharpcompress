namespace SharpCompress.Common.Rar
{
    using SharpCompress.IO;
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class RarCryptoBinaryReader : MarkingBinaryReader
    {
        private readonly Queue<byte> data;
        private readonly string password;
        private RarRijndael rijndael;
        private byte[] salt;

        public RarCryptoBinaryReader(Stream stream, string password) : base(stream)
        {
            this.data = new Queue<byte>();
            this.password = password;
        }

        public void ClearQueue()
        {
            this.data.Clear();
        }

        internal void InitializeAes(byte[] salt)
        {
            this.salt = salt;
            this.rijndael = RarRijndael.InitializeFrom(this.password, salt);
        }

        private byte[] ReadAndDecryptBytes(int count)
        {
            int num4;
            int num = this.data.Count;
            int num2 = count - num;
            if (num2 > 0)
            {
                int num3 = num2 + ((~num2 + 1) & 15);
                for (num4 = 0; num4 < (num3 / 0x10); num4++)
                {
                    byte[] cipherText = base.ReadBytes(0x10);
                    byte[] buffer2 = this.rijndael.ProcessBlock(cipherText);
                    foreach (byte num5 in buffer2)
                    {
                        this.data.Enqueue(num5);
                    }
                }
            }
            byte[] buffer3 = new byte[count];
            for (num4 = 0; num4 < count; num4++)
            {
                buffer3[num4] = this.data.Dequeue();
            }
            return buffer3;
        }

        public override byte[] ReadBytes(int count)
        {
            if (this.UseEncryption)
            {
                return this.ReadAndDecryptBytes(count);
            }
            return base.ReadBytes(count);
        }

        public void SkipQueue()
        {
            long position = this.BaseStream.Position;
            this.BaseStream.Position = position + this.data.Count;
            this.ClearQueue();
        }

        protected bool UseEncryption
        {
            get
            {
                return (this.salt != null);
            }
        }
    }
}

