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
        var body = await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
        await ReadExtendedHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
        return LoadFrom(body);
    }
}
