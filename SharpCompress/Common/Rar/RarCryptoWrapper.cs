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
        private RarRijndael _rijndael;
        private readonly string _password;
        private Queue<byte> _data = new Queue<byte>();

        public RarCryptoWrapper(Stream actualStream, string password)
        {
            _actualStream = actualStream;
            _password = password;
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
            _rijndael = RarRijndael.InitializeFrom(_password, _salt);
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
                    byte[] cipherText = new byte[RarRijndael.CryptoBlockSize];
                    _actualStream.Read(cipherText, 0, RarRijndael.CryptoBlockSize);


                    var readBytes = _rijndael.ProcessBlock(cipherText);
                    foreach(var readByte in readBytes)
                        _data.Enqueue(readByte);


                }

                for (int i = 0; i < count; i++)
                    buffer[offset+i] = _data.Dequeue();
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