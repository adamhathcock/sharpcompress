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
            // Rar old signature: 52 45 7E 5E (not supported)

            // Rar4 signature: 52 61 72 21 1A 07 00
            return HeadCRC == 0x6152 &&
                   HeaderType == HeaderType.MarkHeader &&
                   Flags == 0x1A21 &&
                   HeaderSize == 0x07;

            // Rar5 signature: 52 61 72 21 1A 07 10 00 (not supported yet)
        }

        internal bool OldFormat { get; private set; }
    }
}