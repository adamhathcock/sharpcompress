using System;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace SharpCompress.Crypto
{
    //Gathered from:
    //http://stackoverflow.com/questions/3210795/pbkdf2-in-bouncy-castle-c-sharp and Rfc2898DeriveBytes 
    internal class PBKDF2
    {
        private readonly IMac hMac = new HMac(new Sha1Digest());
        private readonly byte[] state = new byte[20];
        private int endIndex;
        private int startIndex;
        private uint block = 1u;
        private readonly byte[] password;
        private readonly byte[] salt;
        private readonly int iterations;


        public PBKDF2(byte[] password, byte[] salt, int iterations)
        {
            this.password = password;
            this.salt = salt;
            this.iterations = iterations;
        }

        public byte[] GetBytes(int cb)
        {
            if (cb <= 0)
            {
                throw new ArgumentOutOfRangeException("cb");
            }
            byte[] array = new byte[cb];
            int i = 0;
            int num = endIndex - startIndex;
            if (num > 0)
            {
                if (cb < num)
                {
                    Buffer.BlockCopy(state, startIndex, array, 0, cb);
                    startIndex += cb;
                    return array;
                }
                Buffer.BlockCopy(state, startIndex, array, 0, num);
                startIndex = (endIndex = 0);
                i += num;
            }
            while (i < cb)
            {
                byte[] src = Hash();
                int num2 = cb - i;
                if (num2 <= 20)
                {
                    Buffer.BlockCopy(src, 0, array, i, num2);
                    i += num2;
                    Buffer.BlockCopy(src, num2, state, startIndex, 20 - num2);
                    endIndex += 20 - num2;
                    return array;
                }
                Buffer.BlockCopy(src, 0, array, i, 20);
                i += 20;
            }
            return array;
        }

        private byte[] Hash()
        {
            byte[] array = UIntToOctet(block);
            ICipherParameters param = new KeyParameter(password);

            hMac.Init(param);
            hMac.BlockUpdate(salt, 0, salt.Length);

            hMac.BlockUpdate(array, 0, array.Length);
            byte[] array2 = new byte[20];
            hMac.DoFinal(array2, 0);

            hMac.Init(param);

            byte[] array3 = new byte[20];
            Buffer.BlockCopy(array2, 0, array3, 0, 20);
            int num = 2;
            while (num <= (long)((ulong)iterations))
            {
                hMac.BlockUpdate(array2, 0, array2.Length);
                hMac.DoFinal(array2, 0);
                for (int i = 0; i < 20; i++)
                {
                    array3[i] ^= array2[i];
                }
                num++;
            }
            block += 1u;
            return array3;
        }

        internal static byte[] UIntToOctet(uint i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            byte[] result = new[]
                            {
                                bytes[3],
                                bytes[2],
                                bytes[1],
                                bytes[0]
                            };
            if (!BitConverter.IsLittleEndian)
            {
                return bytes;
            }
            return result;
        }

    }
}

