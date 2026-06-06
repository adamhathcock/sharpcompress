using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Archives.Extraction;

internal interface IArchiveExtractionConcurrencyProvider
{
    ValueTask<ArchiveInformation> GetArchiveInformationAsync(
        CancellationToken cancellationToken = default
    );
}
