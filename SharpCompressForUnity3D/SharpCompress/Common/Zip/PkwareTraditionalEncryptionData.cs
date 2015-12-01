namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.Compressor.Deflate;
    using System;
    using System.Text;

    internal class PkwareTraditionalEncryptionData
    {
        private readonly uint[] _Keys = new uint[] { 0x12345678, 0x23456789, 0x34567890 };
        private static readonly CRC32 crc32 = new CRC32();

        private PkwareTraditionalEncryptionData(string password)
        {
            this.Initialize(password);
        }

        public byte[] Decrypt(byte[] cipherText, int length)
        {
            if (length > cipherText.Length)
            {
                throw new ArgumentOutOfRangeException("length", "Bad length during Decryption: the length parameter must be smaller than or equal to the size of the destination array.");
            }
            byte[] buffer = new byte[length];
            for (int i = 0; i < length; i++)
            {
                byte byteValue = (byte) (cipherText[i] ^ this.MagicByte);
                this.UpdateKeys(byteValue);
                buffer[i] = byteValue;
            }
            return buffer;
        }

        public byte[] Encrypt(byte[] plainText, int length)
        {
            if (plainText == null)
            {
                throw new ArgumentNullException("plaintext");
            }
            if (length > plainText.Length)
            {
                throw new ArgumentOutOfRangeException("length", "Bad length during Encryption: The length parameter must be smaller than or equal to the size of the destination array.");
            }
            byte[] buffer = new byte[length];
            for (int i = 0; i < length; i++)
            {
                byte byteValue = plainText[i];
                buffer[i] = (byte) (plainText[i] ^ this.MagicByte);
                this.UpdateKeys(byteValue);
            }
            return buffer;
        }

        public static PkwareTraditionalEncryptionData ForRead(string password, ZipFileEntry header, byte[] encryptionHeader)
        {
            PkwareTraditionalEncryptionData data = new PkwareTraditionalEncryptionData(password);
            byte[] buffer = data.Decrypt(encryptionHeader, encryptionHeader.Length);
            if (buffer[11] != ((byte) ((header.Crc >> 0x18) & 0xff)))
            {
                if (!FlagUtility.HasFlag<HeaderFlags>(header.Flags, HeaderFlags.UsePostDataDescriptor))
                {
                    throw new CryptographicException("The password did not match.");
                }
                if (buffer[11] != ((byte) ((header.LastModifiedTime >> 8) & 0xff)))
                {
                    throw new CryptographicException("The password did not match.");
                }
            }
            return data;
        }

        private void Initialize(string password)
        {
            byte[] buffer = StringToByteArray(password);
            for (int i = 0; i < password.Length; i++)
            {
                this.UpdateKeys(buffer[i]);
            }
        }

        internal static byte[] StringToByteArray(string value)
        {
            return StringToByteArray(value, ArchiveEncoding.Password);
        }

        internal static byte[] StringToByteArray(string value, Encoding encoding)
        {
            return encoding.GetBytes(value);
        }

        private void UpdateKeys(byte byteValue)
        {
            this._Keys[0] = (uint) crc32.ComputeCrc32((int) this._Keys[0], byteValue);
            this._Keys[1] += (byte) this._Keys[0];
            this._Keys[1] = (this._Keys[1] * 0x8088405) + 1;
            this._Keys[2] = (uint) crc32.ComputeCrc32((int) this._Keys[2], (byte) (this._Keys[1] >> 0x18));
        }

        private byte MagicByte
        {
            get
            {
                ushort num = (ushort) (((ushort) (this._Keys[2] & 0xffff)) | 2);
                return (byte) ((num * (num ^ 1)) >> 8);
            }
        }
    }
}

