using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar {
    internal class RarCrcStream : RarStream {
        private readonly MultiVolumeReadOnlyStream readStream;
        private uint currentCrc;

        public RarCrcStream(Unpack unpack, FileHeader fileHeader, MultiVolumeReadOnlyStream readStream) : base(unpack, fileHeader, readStream)
        {
            this.readStream = readStream;
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
            } 
            else if (GetCrc() != this.readStream.CurrentCrc)
            {
                // NOTE: we use the last FileHeader in a multipart volume to check CRC
                throw new InvalidFormatException("file crc mismatch");
            }
            return result;
        }
    }
}