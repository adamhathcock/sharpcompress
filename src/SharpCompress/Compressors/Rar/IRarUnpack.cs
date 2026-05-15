using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar;

internal interface IRarUnpack
{
    void DoUnpack(FileHeader fileHeader, Stream readStream, Stream writeStream);
    void DoUnpack();

    ValueTask DoUnpackAsync(
        FileHeader fileHeader,
        Stream readStream,
        Stream writeStream,
        CancellationToken cancellationToken
    );
    ValueTask DoUnpackAsync(CancellationToken cancellationToken);

    // eg u/i pause/resume button
    bool Suspended { get; set; }

    long DestSize { get; }
    int ReadChar();
    ValueTask<int> ReadCharAsync(CancellationToken cancellationToken);
    int PpmEscChar { get; set; }
}
