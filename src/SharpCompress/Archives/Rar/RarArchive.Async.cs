using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Archives.Rar;

public partial class RarArchive
{
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (UnpackV1.IsValueCreated && UnpackV1.Value is IDisposable unpackV1)
            {
                unpackV1.Dispose();
            }

            _disposed = true;
            await base.DisposeAsync();
        }
    }

    protected override async ValueTask<IAsyncReader> CreateReaderForSolidExtractionAsync()
    {
        if (await this.IsMultipartVolumeAsync())
        {
            var streams = await VolumesAsync
                .Select(volume =>
                {
                    volume.Stream.Position = 0;
                    return volume.Stream;
                })
                .ToListAsync();
            return (RarReader)RarReader.OpenReader(streams, ReaderOptions);
        }

        var stream = (await VolumesAsync.FirstAsync()).Stream;
        stream.Position = 0;
        return (RarReader)RarReader.OpenReader(stream, ReaderOptions);
    }

    public override async ValueTask<bool> IsSolidAsync() =>
        await (await VolumesAsync.CastAsync<RarVolume>().FirstAsync()).IsSolidArchiveAsync();
}
