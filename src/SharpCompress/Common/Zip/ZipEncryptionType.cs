namespace SharpCompress.Common.Zip;

/// <summary>
/// Specifies the encryption method to use when creating encrypted ZIP archives.
/// </summary>
public enum ZipEncryptionType
{
    /// <summary>
    /// No encryption.
    /// </summary>
    None = 0,

    /// <summary>
    /// PKWARE Traditional (ZipCrypto) encryption.
    /// This is the older, less secure encryption method but is widely compatible.
    /// </summary>
    PkwareTraditional = 1,

    /// <summary>
    /// WinZip AES-256 encryption.
    /// This is the more secure encryption method using AES-256.
    /// </summary>
    Aes256 = 2,

    /// <summary>
    /// WinZip AES-128 encryption.
    /// This uses AES-128 for encryption.
    /// </summary>
    Aes128 = 3,
}
