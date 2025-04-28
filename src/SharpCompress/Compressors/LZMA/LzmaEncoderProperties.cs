using System;

namespace SharpCompress.Compressors.LZMA;

public class LzmaEncoderProperties
{
    public static LzmaEncoderProperties Default { get; } = new();

    internal ReadOnlySpan<CoderPropId> PropIDs => _propIDs;
    private readonly CoderPropId[] _propIDs;

    internal ReadOnlySpan<object> Properties => _properties;
    private readonly object[] _properties;

    public LzmaEncoderProperties()
        : this(false) { }

    public LzmaEncoderProperties(bool eos)
        : this(eos, 1 << 20) { }

    public LzmaEncoderProperties(bool eos, int dictionary)
        : this(eos, dictionary, 32) { }

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
            CoderPropId.EndMarker,
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
            eos,
        };
    }
}
