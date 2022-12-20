namespace SharpCompress.Compressors.LZMA;

public class LzmaEncoderProperties
{
    internal CoderPropId[] _propIDs;
    internal object[] _properties;

    public LzmaEncoderProperties() : this(false) { }

    public LzmaEncoderProperties(bool eos) : this(eos, 1 << 20) { }

    public LzmaEncoderProperties(bool eos, int dictionary) : this(eos, dictionary, 32) { }

    public LzmaEncoderProperties(bool eos, int dictionary, int numFastBytes)
    {
        var posStateBits = 2;
        var litContextBits = 3;
        var litPosBits = 0;
        var algorithm = 2;
        var mf = "bt4";

        _propIDs = new[]
        {
            CoderPropId.DictionarySize,
            CoderPropId.PosStateBits,
            CoderPropId.LitContextBits,
            CoderPropId.LitPosBits,
            CoderPropId.Algorithm,
            CoderPropId.NumFastBytes,
            CoderPropId.MatchFinder,
            CoderPropId.EndMarker
        };
        _properties = new object[]
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
