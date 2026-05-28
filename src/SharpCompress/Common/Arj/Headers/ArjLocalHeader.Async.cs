using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Arj.Headers;

public partial class ArjLocalHeader
{
    public override async ValueTask<ArjHeader?> ReadAsync(
        Stream reader,
        CancellationToken cancellationToken = default
    )
    {
        var body = await ReadHeaderAsync(reader, cancellationToken).ConfigureAwait(false);
        if (body.Length > 0)
        {
            await ReadExtendedHeadersAsync(reader, cancellationToken).ConfigureAwait(false);
            var header = LoadFrom(body);
            header.DataStartPosition = reader.Position;
            return header;
        }
        return null;
    }
}
