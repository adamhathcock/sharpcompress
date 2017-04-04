using System.IO;
using SharpCompress.Compressor.Rar;
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

        public override byte[] ReadBytes(int count)
        {
            var result = base.ReadBytes(count);
            this.currentCrc = RarCRC.CheckCrc(this.currentCrc, result, 0, result.Length);
            return result;
        }
    }
}
