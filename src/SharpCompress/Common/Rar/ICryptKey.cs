using System.Security.Cryptography;

namespace SharpCompress.Common.Rar;

internal interface ICryptKey
{
    ICryptoTransform Transformer(byte[] salt);
}
