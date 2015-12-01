namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using System;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;

    internal class WinzipAesEncryptionData
    {
        [CompilerGenerated]
        private byte[] _IvBytes_k__BackingField;
        [CompilerGenerated]
        private byte[] _KeyBytes_k__BackingField;
        private byte[] generatedVerifyValue;
        private WinzipAesKeySize keySize;
        private string password;
        private byte[] passwordVerifyValue;
        private const int RFC2898_ITERATIONS = 0x3e8;
        private byte[] salt;

        internal WinzipAesEncryptionData(WinzipAesKeySize keySize, byte[] salt, byte[] passwordVerifyValue, string password)
        {
            this.keySize = keySize;
            this.salt = salt;
            this.passwordVerifyValue = passwordVerifyValue;
            this.password = password;
            this.Initialize();
        }

        private void Initialize()
        {
            Rfc2898DeriveBytes bytes = new Rfc2898DeriveBytes(this.password, this.salt, 0x3e8);
            this.KeyBytes = bytes.GetBytes(this.KeySizeInBytes);
            this.IvBytes = bytes.GetBytes(this.KeySizeInBytes);
            this.generatedVerifyValue = bytes.GetBytes(2);
            short num = BitConverter.ToInt16(this.passwordVerifyValue, 0);
            if (this.password != null)
            {
                short num2 = BitConverter.ToInt16(this.generatedVerifyValue, 0);
                if (num != num2)
                {
                    throw new InvalidFormatException("bad password");
                }
            }
        }

        internal static int KeyLengthInBytes(WinzipAesKeySize keySize)
        {
            switch (keySize)
            {
                case WinzipAesKeySize.KeySize128:
                    return 0x10;

                case WinzipAesKeySize.KeySize192:
                    return 0x18;

                case WinzipAesKeySize.KeySize256:
                    return 0x20;
            }
            throw new InvalidOperationException();
        }

        internal byte[] IvBytes
        {
            [CompilerGenerated]
            get
            {
                return this._IvBytes_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._IvBytes_k__BackingField = value;
            }
        }

        internal byte[] KeyBytes
        {
            [CompilerGenerated]
            get
            {
                return this._KeyBytes_k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this._KeyBytes_k__BackingField = value;
            }
        }

        private int KeySizeInBytes
        {
            get
            {
                return KeyLengthInBytes(this.keySize);
            }
        }
    }
}

