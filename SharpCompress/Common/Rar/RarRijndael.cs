using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SharpCompress.Common.Rar
{
    class RarRijndael
    {
        internal const int CryptoBlockSize = 16;

        internal static Rijndael Initialize(out byte[] aesInitializationVector, string password, byte[] salt)
        {
            var rijndael = new RijndaelManaged() {Padding = PaddingMode.None};
            aesInitializationVector = new byte[CryptoBlockSize];
            int rawLength = 2 * password.Length;
            byte[] rawPassword = new byte[rawLength + 8];
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            for (int i = 0; i < password.Length; i++)
            {
                rawPassword[i * 2] = passwordBytes[i];
                rawPassword[i * 2 + 1] = 0;
            }
            for (int i = 0; i < salt.Length; i++)
            {
                rawPassword[i + rawLength] = salt[i];
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
                    aesInitializationVector[i / (noOfRounds / CryptoBlockSize)] = digest[19];
                }
            }

            digest = sha.ComputeHash(bytes.ToArray());

            byte[] aesKey = new byte[CryptoBlockSize];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    aesKey[i * 4 + j] = (byte)
                        (((digest[i * 4] * 0x1000000) & 0xff000000 |
                        ((digest[i * 4 + 1] * 0x10000) & 0xff0000) |
                          ((digest[i * 4 + 2] * 0x100) & 0xff00) |
                          digest[i * 4 + 3] & 0xff) >> (j * 8));

            rijndael.IV = new byte[CryptoBlockSize];
            rijndael.Key = aesKey;
            rijndael.BlockSize = CryptoBlockSize * 8;
            return rijndael;
        }
    }
}
