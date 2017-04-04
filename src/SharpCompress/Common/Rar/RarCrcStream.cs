using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressor.Rar {
    internal class RarCrcStream : RarStream {
        private readonly uint expectedCrc;
        private uint currentCrc;

        public RarCrcStream(Unpack unpack, FileHeader fileHeader, Stream readStream) : base(unpack, fileHeader, readStream)
        {
            this.expectedCrc = fileHeader.FileCRC;
            ResetCrc();
        }

        public uint GetCrc()
        {
            return ~this.currentCrc;
        }

        public void ResetCrc()
        {
            this.currentCrc = 0xffffffff;
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            var result = base.Read(buffer, offset, count);
            if (result != 0) 
            {
                this.currentCrc = RarCRC.CheckCrc(this.currentCrc, buffer, offset, result);
            } else if (GetCrc() != this.expectedCrc)
            {
                throw new InvalidFormatException("file crc mismatch");
            }
            return result;
        }
    }
}
