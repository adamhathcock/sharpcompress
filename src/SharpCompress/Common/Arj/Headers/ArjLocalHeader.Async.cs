using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Arj.Headers;

public partial class ArjLocalHeader
{
    public override async ValueTask<ArjHeader?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var body = await ReadHeaderAsync(stream, cancellationToken).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await ReadExtendedHeadersAsync(stream, cancellationToken).ConfigureAwait(false);
            var header = LoadFrom(body);
            header.DataStartPosition = stream.Position;
            return header;
        }
        return null;
    }
}
