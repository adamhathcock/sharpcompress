using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.SevenZip;

public partial class SevenZipArchive
{
    private async ValueTask LoadFactoryAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        if (_database is null)
        {
            stream.Position = 0;
            var reader = new ArchiveReader();
            await reader.OpenAsync(
                stream,
                lookForHeader: ReaderOptions.LookForHeader,
                cancellationToken
            );
            _database = await reader.ReadDatabaseAsync(
                new PasswordProvider(ReaderOptions.Password),
                cancellationToken
            );
        }
    }

    protected override ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync() =>
        new(new SevenZipReader(ReaderOptions, this));
}
