using System;
using System.Text;
using SharpCompress.Converter;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Zip
{
    internal class WinzipAesEncryptionData
    {
        private const int RFC2898_ITERATIONS = 1000;

        private byte[] salt;
        private WinzipAesKeySize keySize;
        private byte[] passwordVerifyValue;
        private string password;

        private byte[] generatedVerifyValue;

        internal WinzipAesEncryptionData(WinzipAesKeySize keySize, byte[] salt, byte[] passwordVerifyValue,
                                         string password)
        {
            this.keySize = keySize;
            this.salt = salt;
            this.passwordVerifyValue = passwordVerifyValue;
            this.password = password;
            Initialize();
        }

        internal byte[] IvBytes { get; set; }
        internal byte[] KeyBytes { get; set; }

        private int KeySizeInBytes
        {
            get { return KeyLengthInBytes(keySize); }
        }

        internal static int KeyLengthInBytes(WinzipAesKeySize keySize)
        {
            switch (keySize)
            {
                case WinzipAesKeySize.KeySize128:
                    return 16;
                case WinzipAesKeySize.KeySize192:
                    return 24;
                case WinzipAesKeySize.KeySize256:
                    return 32;
            }
            throw new InvalidOperationException();
        }

        private void Initialize()
        {
            var utf8 = new UTF8Encoding(false);
            var paramz = new PBKDF2(utf8.GetBytes(password), salt, RFC2898_ITERATIONS);
            KeyBytes = paramz.GetBytes(KeySizeInBytes);
            IvBytes = paramz.GetBytes(KeySizeInBytes);
            generatedVerifyValue = paramz.GetBytes(2);


            short verify = DataConverter.LittleEndian.GetInt16(passwordVerifyValue, 0);
            if (password != null)
            {
                short generated = DataConverter.LittleEndian.GetInt16(generatedVerifyValue, 0);
                if (verify != generated)
                    throw new InvalidFormatException("bad password");
            }
        }
    }
}