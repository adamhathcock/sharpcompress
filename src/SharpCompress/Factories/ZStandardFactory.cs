using System.Collections.Generic;
using System.IO;
using SharpCompress.Compressors.ZStandard;

namespace SharpCompress.Factories;

internal class ZStandardFactory : Factory
{
    public override string Name => "ZStandard";

    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "zst";
        yield return "zstd";
    }

    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = 65536
    ) => ZStandardStream.IsZStandard(stream);
}
