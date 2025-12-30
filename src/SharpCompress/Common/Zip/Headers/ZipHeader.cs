using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers;

internal abstract class ZipHeader(ZipHeaderType type)
{
    internal ZipHeaderType ZipHeaderType { get; } = type;

    internal abstract ValueTask Read(AsyncBinaryReader reader);

    internal bool HasData { get; set; } = true;
}
