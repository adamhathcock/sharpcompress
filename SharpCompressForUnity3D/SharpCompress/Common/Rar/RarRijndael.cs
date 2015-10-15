namespace SharpCompress.Common.Rar
{
    using SharpCompress;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;

    internal class RarRijndael : IDisposable
    {
        private byte[] aesInitializationVector;
        internal const int CRYPTO_BLOCK_SIZE = 0x10;
        private readonly string password;
        private Rijndael rijndael;
        private readonly byte[] salt;

        private RarRijndael(string password, byte[] salt)
        {
            this.password = password;
            this.salt = salt;
        }

        public void Dispose()
        {
            ((IDisposable) this.rijndael).Dispose();
        }

        private void Initialize()
        {
            int num2;
            byte[] buffer3;
            RijndaelManaged managed = new RijndaelManaged();
            managed.Padding = PaddingMode.None;
            this.rijndael = managed;
            this.aesInitializationVector = new byte[0x10];
            int num = 2 * this.password.Length;
            byte[] source = new byte[num + 8];
            byte[] bytes = Encoding.Unicode.GetBytes(this.password);
            for (num2 = 0; num2 < num; num2++)
            {
                source[num2] = bytes[num2];
            }
            for (num2 = 0; num2 < this.salt.Length; num2++)
            {
                source[num2 + num] = this.salt[num2];
            }
            SHA1 sha = new SHA1CryptoServiceProvider();
            IList<byte> destination = new List<byte>();
            for (num2 = 0; num2 < 0x40000; num2++)
            {
                Utility.AddRange<byte>(destination, source);
                Utility.AddRange<byte>(destination, new byte[] { (byte) num2, (byte) (num2 >> 8), (byte) (num2 >> 0x10) });
                if ((num2 % 0x4000) == 0)
                {
                    buffer3 = sha.ComputeHash(Enumerable.ToArray<byte>(destination));
                    this.aesInitializationVector[num2 / 0x4000] = buffer3[0x13];
                }
            }
            buffer3 = sha.ComputeHash(Enumerable.ToArray<byte>(destination));
            byte[] buffer4 = new byte[0x10];
            for (num2 = 0; num2 < 4; num2++)
            {
                for (int i = 0; i < 4; i++)
                {
                    buffer4[(num2 * 4) + i] = (byte) ((((((buffer3[num2 * 4] * 0x1000000) & 0xff000000L) | ((ulong) ((buffer3[(num2 * 4) + 1] * 0x10000) & 0xff0000))) | ((ulong) ((buffer3[(num2 * 4) + 2] * 0x100) & 0xff00))) | ((ulong) (buffer3[(num2 * 4) + 3] & 0xff))) >> (i * 8));
                }
            }
            this.rijndael.IV = new byte[0x10];
            this.rijndael.Key = buffer4;
            this.rijndael.BlockSize = 0x80;
        }

        public static RarRijndael InitializeFrom(string password, byte[] salt)
        {
            RarRijndael rijndael = new RarRijndael(password, salt);
            rijndael.Initialize();
            return rijndael;
        }

        public byte[] ProcessBlock(byte[] cipherText)
        {
            byte[] buffer = new byte[0x10];
            List<byte> list = new List<byte>();
            ICryptoTransform transform = this.rijndael.CreateDecryptor();
            using (MemoryStream stream = new MemoryStream(cipherText))
            {
                using (CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Read))
                {
                    Utility.ReadFully(stream2, buffer);
                }
            }
            int index = 0;
            while (index < buffer.Length)
            {
                list.Add((byte) (buffer[index] ^ this.aesInitializationVector[index % 0x10]));
                index++;
            }
            for (index = 0; index < this.aesInitializationVector.Length; index++)
            {
                this.aesInitializationVector[index] = cipherText[index];
            }
            return list.ToArray();
        }
    }
}

