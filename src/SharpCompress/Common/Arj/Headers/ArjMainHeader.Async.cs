using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Arj.Headers;

public partial class ArjMainHeader
{
    public override async ValueTask<ArjHeader?> ReadAsync(
        Stream reader,
        CancellationToken cancellationToken = default
    )
    {
        var body = await ReadHeaderAsync(reader, cancellationToken).ConfigureAwait(false);
        await ReadExtendedHeadersAsync(reader, cancellationToken).ConfigureAwait(false);
        return LoadFrom(body);
    }
}
