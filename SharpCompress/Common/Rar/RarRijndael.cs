using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SharpCompress.Common.Rar
{
    class RarRijndael : IDisposable
    {
        private readonly string _password;
        private readonly byte[] _salt;
        private byte[] _aesInitializationVector;
        private Rijndael _rijndael;

        private RarRijndael(string password, byte[] salt)
        {
            _password = password;
            _salt = salt;
        }

        internal const int CryptoBlockSize = 16;

        private void Initialize()
        {
            _rijndael = new RijndaelManaged() { Padding = PaddingMode.None };
            _aesInitializationVector = new byte[CryptoBlockSize];
            int rawLength = 2 * _password.Length;
            byte[] rawPassword = new byte[rawLength + 8];
            byte[] passwordBytes = Encoding.UTF8.GetBytes(_password);
            for (int i = 0; i < _password.Length; i++)
            {
                rawPassword[i * 2] = passwordBytes[i];
                rawPassword[i * 2 + 1] = 0;
            }
            for (int i = 0; i < _salt.Length; i++)
            {
                rawPassword[i + rawLength] = _salt[i];
            }

            var sha = new SHA1Managed();

            const int noOfRounds = (1 << 18);
            IList<byte> bytes = new List<byte>();
            byte[] digest;
            for (int i = 0; i < noOfRounds; i++)
            {
                bytes.AddRange(rawPassword);

                bytes.AddRange(new[] { (byte)i, (byte)(i >> 8), (byte)(i >> CryptoBlockSize) });
                if (i % (noOfRounds / CryptoBlockSize) == 0)
                {
                    digest = sha.ComputeHash(bytes.ToArray());
                    _aesInitializationVector[i / (noOfRounds / CryptoBlockSize)] = digest[19];
                }
            }

            digest = sha.ComputeHash(bytes.ToArray());

            byte[] aesKey = new byte[CryptoBlockSize];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    aesKey[i * 4 + j] = (byte)
                        (((digest[i * 4] * 0x1000000) & 0xff000000 |
                        (uint)((digest[i * 4 + 1] * 0x10000) & 0xff0000) |
                          (uint)((digest[i * 4 + 2] * 0x100) & 0xff00) |
                          (uint)(digest[i * 4 + 3] & 0xff)) >> (j * 8));

            _rijndael.IV = new byte[CryptoBlockSize];
            _rijndael.Key = aesKey;
            _rijndael.BlockSize = CryptoBlockSize * 8;
        }

        public static RarRijndael InitializeFrom(string password, byte[] salt)
        {
            var rijndael = new RarRijndael(password, salt);
            rijndael.Initialize();
            return rijndael;
        }

        public byte[] ProcessBlock(byte[] cipherText)
        {
            var plainText = new byte[CryptoBlockSize];
            var decryptedBytes = new List<byte>();
            var decryptor = _rijndael.CreateDecryptor();
            using (var msDecrypt = new MemoryStream(cipherText))
            {
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    csDecrypt.ReadFully(plainText);
                }
            }

            for (int j = 0; j < plainText.Length; j++)
                decryptedBytes.Add((byte)(plainText[j] ^ _aesInitializationVector[j % 16])); //32:114, 33:101

            for (int j = 0; j < _aesInitializationVector.Length; j++)
                _aesInitializationVector[j] = cipherText[j];

            return decryptedBytes.ToArray();
        }

        public void Dispose()
        {
            _rijndael.Dispose();
        }
    }
}
