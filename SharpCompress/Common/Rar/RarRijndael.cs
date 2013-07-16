﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SharpCompress.Common.Rar
{
    internal class RarRijndael : IDisposable
    {
        private readonly string password;
        private readonly byte[] salt;
        private byte[] aesInitializationVector;
        private Rijndael rijndael;

        private RarRijndael(string password, byte[] salt)
        {
            this.password = password;
            this.salt = salt;
        }

        internal const int CryptoBlockSize = 16;

        private void Initialize()
        {

            rijndael = new RijndaelManaged() { Padding = PaddingMode.None };
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

            SHA1 sha = new SHA1CryptoServiceProvider();

            const int noOfRounds = (1 << 18);
            IList<byte> bytes = new List<byte>();
            byte[] digest;

            //TODO slow code below, find ways to optimize
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
            //slow code ends

            byte[] aesKey = new byte[CryptoBlockSize];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    aesKey[i * 4 + j] = (byte)
                        (((digest[i * 4] * 0x1000000) & 0xff000000 |
                        (uint)((digest[i * 4 + 1] * 0x10000) & 0xff0000) |
                          (uint)((digest[i * 4 + 2] * 0x100) & 0xff00) |
                          (uint)(digest[i * 4 + 3] & 0xff)) >> (j * 8));

            rijndael.IV = new byte[CryptoBlockSize];
            rijndael.Key = aesKey;
            rijndael.BlockSize = CryptoBlockSize * 8;
            
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
            var decryptor = rijndael.CreateDecryptor();
            using (var msDecrypt = new MemoryStream(cipherText))
            {
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    csDecrypt.ReadFully(plainText);
                }
            }

            for (int j = 0; j < plainText.Length; j++)
                decryptedBytes.Add((byte)(plainText[j] ^ aesInitializationVector[j % 16])); //32:114, 33:101

            for (int j = 0; j < aesInitializationVector.Length; j++)
                aesInitializationVector[j] = cipherText[j];
            return decryptedBytes.ToArray();
        }

        public void Dispose()
        {
            rijndael.Dispose();
        }
    }
}
