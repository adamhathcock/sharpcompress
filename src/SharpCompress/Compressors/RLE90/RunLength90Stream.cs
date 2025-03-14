using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.RLE90
{
    public class RunLength90Stream : Stream
    {
        private readonly Stream _stream;
        private const byte DLE = 0x90;
        private int _compressedSize;
        private bool _processed = false;

        public RunLength90Stream(Stream stream, int compressedSize)
        {
            _stream = stream;
            _compressedSize = compressedSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => _stream.Position;
            set => throw new NotImplementedException();
        }

        public override void Flush() => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_processed)
            {
                return 0;
            }
            _processed = true;

            using var binaryReader = new BinaryReader(_stream);
            byte[] compressedBuffer = binaryReader.ReadBytes(_compressedSize);

            var unpacked = RLE.UnpackRLE(compressedBuffer);
            unpacked.CopyTo(buffer);

            return unpacked.Count;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();
    }
}
