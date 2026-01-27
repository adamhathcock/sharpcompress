using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Arj.Headers;

public partial class ArjMainHeader
{
    public override async ValueTask<ArjHeader?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var body = await ReadHeaderAsync(stream, cancellationToken);
        await ReadExtendedHeadersAsync(stream, cancellationToken);
        return LoadFrom(body);
    }
}
