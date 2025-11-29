using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Ace;

namespace SharpCompress.Factories;

/// <summary>
/// Factory for ACE archive format detection and reader creation.
/// </summary>
public class AceFactory : Factory, IReaderFactory
{
    // ACE signature: bytes at offset 7 should be "**ACE**"
    private static readonly byte[] AceSignature =
    [
        (byte)'*',
        (byte)'*',
        (byte)'A',
        (byte)'C',
        (byte)'E',
        (byte)'*',
        (byte)'*',
    ];

    public override string Name => "Ace";

    public override ArchiveType? KnownArchiveType => ArchiveType.Ace;

    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "ace";
    }

    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    )
    {
        // ACE files have a specific signature
        // First two bytes are typically 0x60 0xEA (signature bytes)
        // At offset 7, there should be "**ACE**" (7 bytes)
        var bytes = new byte[14];
        if (stream.Read(bytes, 0, 14) != 14)
        {
            return false;
        }

        // Check for "**ACE**" at offset 7
        for (int i = 0; i < AceSignature.Length; i++)
        {
            if (bytes[7 + i] != AceSignature[i])
            {
                return false;
            }
        }

        return true;
    }

    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        AceReader.Open(stream, options);
}
