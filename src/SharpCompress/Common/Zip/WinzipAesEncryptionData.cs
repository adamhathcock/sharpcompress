#if !NO_CRYPTO
using System;
using System.Security.Cryptography;
using SharpCompress.Converter;
using System.Text;

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

        internal byte[] IvBytes
{
    get; set;
}
        internal byte[] KeyBytes
{
    get; set;
}

        private int KeySizeInBytes
        {
            get { return KeyLengthInBytes(keySize);
}
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
            var rfc2898 = new Rfc2898DeriveBytes(password, salt, RFC2898_ITERATIONS);

            KeyBytes = rfc2898.GetBytes(KeySizeInBytes); // 16 or 24 or 32 ???
            IvBytes = rfc2898.GetBytes(KeySizeInBytes);
            generatedVerifyValue = rfc2898.GetBytes(2);

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
#endif