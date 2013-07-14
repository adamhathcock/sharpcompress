using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SharpCompress.Common.Rar
{
    internal class RarCryptoWrapper : Stream
    {
        private readonly Stream _actualStream;
        private byte[] _salt;
        private Rijndael _rijndael;
        private string _password = "test";
        private byte[] _aesInitializationVector = new byte[CryptoBlockSize];
        private byte[] _aesKey = new byte[CryptoBlockSize];
        private Queue<byte> _data = new Queue<byte>();
        private const int CryptoBlockSize = 16;

        public RarCryptoWrapper(Stream actualStream)
        {
            _actualStream = actualStream;
        }

        internal byte[] Salt
        {
            get { return _salt; }
            set
            {
                _salt = value;
                if (value != null) InitializeAes();

            }
        }

        private void InitializeAes()
        {
            _rijndael = new RijndaelManaged() { Padding = PaddingMode.None };
            int rawLength = 2 * _password.Length;
            byte[] rawPassword = new byte[rawLength + 8];
            byte[] passwordBytes = Encoding.UTF8.GetBytes(_password);
            for (int i = 0; i < _password.Length; i++)
            {
                rawPassword[i * 2] = passwordBytes[i];
                rawPassword[i * 2 + 1] = 0;
            }
            for (int i = 0; i < _salt.Length; i++)
            {
                rawPassword[i + rawLength] = _salt[i];
            }

            var sha = new SHA1Managed();

            const int noOfRounds = (1 << 18);
            IList<byte> bytes = new List<byte>();
            byte[] digest;
            for (int i = 0; i < noOfRounds; i++)
            {
                bytes.AddRange(rawPassword);

                bytes.AddRange(new[] { (byte)i, (byte)(i >> 8), (byte)(i >> CryptoBlockSize) });
                if (i % (noOfRounds / CryptoBlockSize) == 0)
                {
                    digest = sha.ComputeHash(bytes.ToArray());
                    _aesInitializationVector[i / (noOfRounds / CryptoBlockSize)] = digest[19];
                }
            }

            digest = sha.ComputeHash(bytes.ToArray());

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    _aesKey[i * 4 + j] = (byte)
                        (((digest[i * 4] * 0x1000000) & 0xff000000 |
                        ((digest[i * 4 + 1] * 0x10000) & 0xff0000) |
                          ((digest[i * 4 + 2] * 0x100) & 0xff00) |
                          digest[i * 4 + 3] & 0xff) >> (j * 8));

            _rijndael.IV = new byte[CryptoBlockSize];
            _rijndael.Key = _aesKey;
            _rijndael.BlockSize = CryptoBlockSize * 8;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Salt == null) return _actualStream.Read(buffer, offset, count);
            return ReadAndDecrypt(buffer, offset, count);
        }

        public int ReadAndDecrypt(byte[] buffer, int offset, int count)
        {
            int queueSize = _data.Count;
            int sizeToRead = count - queueSize;

            if (sizeToRead > 0)
            {
                int alignedSize = sizeToRead + ((~sizeToRead + 1) & 0xf);
                for (int i = 0; i < alignedSize/16; i++)
                {
                    //long ax = System.currentTimeMillis();
                    byte[] cipherText = new byte[CryptoBlockSize];
                    _actualStream.Read(cipherText, 0, CryptoBlockSize);

                    byte[] plainText = new byte[CryptoBlockSize];
                    var decryptor = _rijndael.CreateDecryptor();
                    using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        {
                            csDecrypt.ReadFully(plainText);
                        }
                    }


                    for (int j = 0; j < plainText.Length; j++)
                    {
                        _data.Enqueue((byte) (plainText[j] ^ _aesInitializationVector[j%16])); //32:114, 33:101

                    }

                    for (int j = 0; j < _aesInitializationVector.Length; j++)
                    {
                        _aesInitializationVector[j] = cipherText[j];
                    }
                }

                for (int i = 0; i < count; i++)
                {
                    buffer[offset+i] = _data.Dequeue();
                    Console.Write(buffer[i].ToString("x2") + " ");
                }
                Console.WriteLine();
            }
            return count;

        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanSeek
        {
            get { throw new NotImplementedException(); }
        }

        public override bool CanWrite
        {
            get { throw new NotImplementedException(); }
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position { get; set; }

        protected override void Dispose(bool disposing)
        {
            if(_rijndael!= null) _rijndael.Dispose();
            base.Dispose(disposing);
        }
    }
}