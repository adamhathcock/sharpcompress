using System;
using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal class SplitHeader : ZipHeader
{
    public SplitHeader()
        : base(ZipHeaderType.Split) { }

    internal override void Read(BinaryReader reader) => throw new NotImplementedException();

    internal override ValueTask Read(AsyncBinaryReader reader) =>
        throw new NotImplementedException();
}
