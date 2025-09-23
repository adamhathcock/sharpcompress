using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.Readers;

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
