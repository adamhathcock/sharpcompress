namespace SharpCompress.Compressor.LZMA.LZ
{
    using System;

    internal class CRC
    {
        private uint _value = uint.MaxValue;
        public static readonly uint[] Table = new uint[0x100];

        static CRC()
        {
            for (uint i = 0; i < 0x100; i++)
            {
                uint num2 = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((num2 & 1) != 0)
                    {
                        num2 = (num2 >> 1) ^ 0xedb88320;
                    }
                    else
                    {
                        num2 = num2 >> 1;
                    }
                }
                Table[i] = num2;
            }
        }

        private static uint CalculateDigest(byte[] data, uint offset, uint size)
        {
            CRC crc = new CRC();
            crc.Update(data, offset, size);
            return crc.GetDigest();
        }

        public uint GetDigest()
        {
            return (this._value ^ uint.MaxValue);
        }

        public void Init()
        {
            this._value = uint.MaxValue;
        }

        public void Update(byte[] data, uint offset, uint size)
        {
            for (uint i = 0; i < size; i++)
            {
                this._value = Table[((byte) this._value) ^ data[offset + i]] ^ (this._value >> 8);
            }
        }

        public void UpdateByte(byte b)
        {
            this._value = Table[((byte) this._value) ^ b] ^ (this._value >> 8);
        }

        private static bool VerifyDigest(uint digest, byte[] data, uint offset, uint size)
        {
            return (CalculateDigest(data, offset, size) == digest);
        }
    }
}

