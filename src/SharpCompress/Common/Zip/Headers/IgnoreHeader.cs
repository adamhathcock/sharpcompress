using System.IO;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal class IgnoreHeader : ZipHeader
{
    public IgnoreHeader(ZipHeaderType type)
        : base(type) { }

    internal override void Read(BinaryReader reader) { }

    internal override ValueTask Read(AsyncBinaryReader reader) => default;
}
