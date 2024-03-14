namespace SharpCompress.Common.SevenZip;

internal readonly struct CMethodId
{
    public const ulong K_COPY_ID = 0;
    public const ulong K_LZMA_ID = 0x030101;
    public const ulong K_LZMA2_ID = 0x21;
    public const ulong K_AES_ID = 0x06F10701;

    public static readonly CMethodId K_COPY = new(K_COPY_ID);
    public static readonly CMethodId K_LZMA = new(K_LZMA_ID);
    public static readonly CMethodId K_LZMA2 = new(K_LZMA2_ID);
    public static readonly CMethodId K_AES = new(K_AES_ID);

    public readonly ulong _id;

    public CMethodId(ulong id) => _id = id;

    public override int GetHashCode() => _id.GetHashCode();

    public override bool Equals(object? obj) => obj is CMethodId other && Equals(other);

    public bool Equals(CMethodId other) => _id == other._id;

    public static bool operator ==(CMethodId left, CMethodId right) => left._id == right._id;

    public static bool operator !=(CMethodId left, CMethodId right) => left._id != right._id;

    public int GetLength()
    {
        var bytes = 0;
        for (var value = _id; value != 0; value >>= 8)
        {
            bytes++;
        }
        return bytes;
    }
}
