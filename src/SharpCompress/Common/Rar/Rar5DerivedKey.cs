namespace SharpCompress.Common.Rar;

/// <summary>
/// Key material produced by one RAR5 PBKDF2 derivation.
/// </summary>
internal sealed class Rar5DerivedKey(byte[] aesKey, byte[] hashKey, byte[] pswCheck)
{
    public byte[] AesKey { get; } = aesKey;

    public byte[] HashKey { get; } = hashKey;

    public byte[] PswCheck { get; } = pswCheck;
}
