using System;
using System.Text;

namespace SharpCompress.Common;

public class ArchiveEncoding : IArchiveEncoding
{
    public Encoding Default { get; set; } = Encoding.Default;
    public Encoding Password { get; set; } = Encoding.Default;
    public Encoding UTF8 { get; set; } = Encoding.UTF8;
    public Encoding? Forced { get; set; }
    public Func<byte[], int, int, EncodingType, string>? CustomDecoder { get; set; }
}
