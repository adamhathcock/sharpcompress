namespace SharpCompress.Compressors.LZMA
{
    public class LzmaEncoderProperties
    {
        internal CoderPropID[] propIDs;
        internal object[] properties;

        public LzmaEncoderProperties()
            : this(false)
        {
        }

        public LzmaEncoderProperties(bool eos)
            : this(eos, 1 << 20)
        {
        }

        public LzmaEncoderProperties(bool eos, int dictionary)
            : this(eos, dictionary, 32)
        {
        }

        public LzmaEncoderProperties(bool eos, int dictionary, int numFastBytes)
        {
            int posStateBits = 2;
            int litContextBits = 3;
            int litPosBits = 0;
            int algorithm = 2;
            string mf = "bt4";

            propIDs = new[]
                      {
                          CoderPropID.DictionarySize,
                          CoderPropID.PosStateBits,
                          CoderPropID.LitContextBits,
                          CoderPropID.LitPosBits,
                          CoderPropID.Algorithm,
                          CoderPropID.NumFastBytes,
                          CoderPropID.MatchFinder,
                          CoderPropID.EndMarker
                      };
            properties = new object[]
                         {
                             dictionary,
                             posStateBits,
                             litContextBits,
                             litPosBits,
                             algorithm,
                             numFastBytes,
                             mf,
                             eos
                         };
        }
    }
}