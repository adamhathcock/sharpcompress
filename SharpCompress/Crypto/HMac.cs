using System;
using System.Collections;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;

namespace Org.BouncyCastle.Crypto.Macs
{
    /**
    * HMAC implementation based on RFC2104
    *
    * H(K XOR opad, H(K XOR ipad, text))
    */
    public class HMac
		: IMac
    {
        private const byte IPAD = (byte)0x36;
        private const byte OPAD = (byte)0x5C;

        private readonly IDigest digest;
        private readonly int digestSize;
        private readonly int blockLength;

		private readonly byte[] inputPad;
        private readonly byte[] outputPad;

        public HMac(
            IDigest digest)
        {
            this.digest = digest;
            this.digestSize = digest.GetDigestSize();
            this.blockLength = digest.GetByteLength();
            this.inputPad = new byte[blockLength];
            this.outputPad = new byte[blockLength];
        }

        public string AlgorithmName
        {
            get { return digest.AlgorithmName + "/HMAC"; }
        }

		public IDigest GetUnderlyingDigest()
        {
            return digest;
        }

        public void Init(
            ICipherParameters parameters)
        {
            digest.Reset();

            byte[] key = ((KeyParameter)parameters).GetKey();
			int keyLength = key.Length;

            if (keyLength > blockLength)
            {
                digest.BlockUpdate(key, 0, key.Length);
                digest.DoFinal(inputPad, 0);

				keyLength = digestSize;
            }
            else
            {
				Array.Copy(key, 0, inputPad, 0, keyLength);
            }

			Array.Clear(inputPad, keyLength, blockLength - keyLength);
            Array.Copy(inputPad, 0, outputPad, 0, blockLength);

			xor(inputPad, IPAD);
			xor(outputPad, OPAD);

			// Initialise the digest
			digest.BlockUpdate(inputPad, 0, inputPad.Length);
        }

        public int GetMacSize()
        {
            return digestSize;
        }

        public void Update(
            byte input)
        {
            digest.Update(input);
        }

        public void BlockUpdate(
            byte[] input,
            int inOff,
            int len)
        {
            digest.BlockUpdate(input, inOff, len);
        }

        public int DoFinal(
            byte[] output,
            int outOff)
        {
            byte[] tmp = new byte[digestSize];
            digest.DoFinal(tmp, 0);

            digest.BlockUpdate(outputPad, 0, outputPad.Length);
            digest.BlockUpdate(tmp, 0, tmp.Length);

            int len = digest.DoFinal(output, outOff);

			// Initialise the digest
            digest.BlockUpdate(inputPad, 0, inputPad.Length);

            return len;
        }

        /**
        * Reset the mac generator.
        */
        public void Reset()
        {
			// Reset underlying digest
            digest.Reset();

			// Initialise the digest
            digest.BlockUpdate(inputPad, 0, inputPad.Length);
        }

		private static void xor(byte[] a, byte n)
		{
			for (int i = 0; i < a.Length; ++i)
            {
                a[i] ^= n;
            }
		}
    }
}
