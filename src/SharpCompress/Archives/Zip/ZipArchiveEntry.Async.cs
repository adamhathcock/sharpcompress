using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip;

namespace SharpCompress.Archives.Zip;

public partial class ZipArchiveEntry
{
    public async ValueTask<Stream> OpenEntryStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        var part = Parts.Single();
        if (part is SeekableZipFilePart seekablePart)
        {
            return (await seekablePart.GetCompressedStreamAsync(cancellationToken)).NotNull();
        }
        return OpenEntryStream();
    }
}
