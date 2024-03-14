#nullable disable

using System;
using System.Security.Cryptography;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers;

internal class ArchiveCryptHeader : RarHeader
{
    public ArchiveCryptHeader(RarHeader header, RarCrcBinaryReader reader)
        : base(header, reader, HeaderType.Crypt) { }

    public Rar5CryptoInfo CryptInfo = new();

    protected override void ReadFinish(MarkingBinaryReader reader) =>
        CryptInfo = new Rar5CryptoInfo(reader, false);
}
