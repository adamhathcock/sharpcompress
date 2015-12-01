namespace SharpCompress.Compressor.LZMA
{
    using System;

    public class LzmaEncoderProperties
    {
        internal object[] properties;
        internal CoderPropID[] propIDs;

        public LzmaEncoderProperties() : this(false)
        {
        }

        public LzmaEncoderProperties(bool eos) : this(eos, 0x100000)
        {
        }

        public LzmaEncoderProperties(bool eos, int dictionary) : this(eos, dictionary, 0x20)
        {
        }

        public LzmaEncoderProperties(bool eos, int dictionary, int numFastBytes)
        {
            int num = 2;
            int num2 = 3;
            int num3 = 0;
            int num4 = 2;
            string str = "bt4";
            this.propIDs = new CoderPropID[] { CoderPropID.DictionarySize, CoderPropID.PosStateBits, CoderPropID.LitContextBits, CoderPropID.LitPosBits, CoderPropID.Algorithm, CoderPropID.NumFastBytes, CoderPropID.MatchFinder, CoderPropID.EndMarker };
            this.properties = new object[] { dictionary, num, num2, num3, num4, numFastBytes, str, eos };
        }
    }
}

