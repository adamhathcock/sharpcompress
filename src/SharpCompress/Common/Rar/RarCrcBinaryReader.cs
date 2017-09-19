using System.IO;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar {
    internal class RarCrcBinaryReader : MarkingBinaryReader {
        private uint currentCrc;

        public RarCrcBinaryReader(Stream stream) : base(stream)
        {
        }

        public ushort GetCrc() 
        {
            return (ushort)~currentCrc;
        }

        public void ResetCrc()
        {
            currentCrc = 0xffffffff;
        }

        protected void UpdateCrc(byte b) 
        {
            currentCrc = RarCRC.CheckCrc(currentCrc, b);
        }

        protected byte[] ReadBytesNoCrc(int count)
        {
            return base.ReadBytes(count);
        }

        public override byte[] ReadBytes(int count)
        {
            var result = base.ReadBytes(count);
            currentCrc = RarCRC.CheckCrc(currentCrc, result, 0, result.Length);
            return result;
        }
    }
}