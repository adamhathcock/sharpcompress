using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class MarkHeader : RarHeader
    {

        protected override void ReadFromReader(MarkingBinaryReader reader)
        {
        }

        internal bool IsValid()
        {
            if (!(HeadCRC == 0x6152))
            {
                return false;
            }
            if (!(HeaderType == HeaderType.MarkHeader))
            {
                return false;
            }
            if (!(Flags == 0x1a21))
            {
                return false;
            }
            if (!(HeaderSize == BaseBlockSize))
            {
                return false;
            }
            return true;
        }

        internal bool IsSignature()
        {
            bool valid = false;
            /*byte[] d = new byte[BaseBlock.BaseBlockSize];
                BinaryWriter writer = new BinaryWriter();
                writer.Write(HeadCRC);
                writer.Write((byte)HeaderType);
                writer.Write(flags);
                writer.Write(HeaderSize);
                writer.Flush
        
            if (d[0] == 0x52) {
                if (d[1]==0x45 && d[2]==0x7e && d[3]==0x5e) {
                    oldFormat=true;
                    valid=true;
                }
                else if (d[1]==0x61 && d[2]==0x72 && d[3]==0x21 && d[4]==0x1a &&
                        d[5]==0x07 && d[6]==0x00) {
                    oldFormat=false;
                    valid=true;
                }
            }*/
            return valid;
        }

        internal bool OldFormat
        {
            get;
            private set;
        }
    }
}
