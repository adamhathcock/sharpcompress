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
            return (ushort)~this.currentCrc;
        }

        public void ResetCrc()
        {
            this.currentCrc = 0xffffffff;
        }

        protected void UpdateCrc(byte b) 
        {
            this.currentCrc = RarCRC.CheckCrc(this.currentCrc, b);
        }

        protected byte[] ReadBytesNoCrc(int count)
        {
            return base.ReadBytes(count);
        }

        public override byte[] ReadBytes(int count)
        {
            var result = base.ReadBytes(count);
            this.currentCrc = RarCRC.CheckCrc(this.currentCrc, result, 0, result.Length);
            return result;
        }
    }
}