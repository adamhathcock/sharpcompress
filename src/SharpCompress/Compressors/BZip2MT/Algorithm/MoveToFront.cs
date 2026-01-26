// Bzip2 library for .net
// By Jaime Olivares
// Location: http://github.com/jaime-olivares/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2

using System;

namespace SharpCompress.Compressors.BZip2MT.Algorithm
{
    /// <summary>
    /// A 256 entry Move To Front transform
    /// </summary>
    internal class MoveToFront
    {
        /// <summary>The Move To Front list</summary>
        private readonly byte[] mtf;

        /// <summary>Public constructor</summary>
        public MoveToFront()
        {
            this.mtf = new byte[256];
            for (int i = 0; i < 256; i++)
                this.mtf[i] = (byte)i;
        }

        /// <summary>Moves a value to the head of the MTF list (forward Move To Front transform)</summary>
        /// <param name="value">The value to move</param>
        /// <return>The position the value moved from</return>
        public int ValueToFront(byte value)
        {
            int index = 0;
            byte temp = this.mtf[0];

            if (value == temp)
                return index;

            this.mtf[0] = value;
            while (temp != value)
            {
                index++;
                byte temp2 = temp;
                temp = this.mtf[index];
                this.mtf[index] = temp2;
            }

            return index;
        }

        /// <summary>Gets the value from a given index and moves it to the front of the MTF list (inverse Move To Front transform)</summary>
        /// <param name="index">The index to move</param>
        /// <return>The value at the given index</return>
        public byte IndexToFront(int index)
        {
            byte value = this.mtf[index];
            Array.ConstrainedCopy(this.mtf, 0, this.mtf, 1, index);
            this.mtf[0] = value;

            return value;
        }
    }
}
