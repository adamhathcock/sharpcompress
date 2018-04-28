using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class CryptHeader : RarHeader
    {
        
        private const int CRYPT_VERSION = 0; // Supported encryption version.
        private const int SIZE_SALT50 = 16;
        private const int SIZE_PSWCHECK = 8;
        private const int SIZE_PSWCHECK_CSUM = 4;
        
        
        private bool UsePswCheck;
        private uint Lg2Count; // Log2 of PBKDF2 repetition count.
        private byte[] Salt;
        private byte[] PswCheck;
        
        public CryptHeader(RarHeader header, RarCrcBinaryReader reader)
            : base(header, reader, HeaderType.Crypt)
        {
        }

        protected override void ReadFinish(MarkingBinaryReader reader)
        {
            var cryptVersion = reader.ReadRarVIntUInt32();
            if (cryptVersion > CRYPT_VERSION)
            {
                //error?
                return;
            }

            UsePswCheck = HasHeaderFlag(EncryptionFlagsV5.CHFL_CRYPT_PSWCHECK);
            Lg2Count    = reader.ReadRarVIntByte(1);
            if (Lg2Count > 0)
            {
                //error?
                return;
            }

            Salt = reader.ReadBytes(SIZE_SALT50);
            if (UsePswCheck)
            {
                PswCheck = reader.ReadBytes(SIZE_PSWCHECK);
                
                var csum = reader.ReadBytes(SIZE_PSWCHECK_CSUM);
            }
        }
    }
}