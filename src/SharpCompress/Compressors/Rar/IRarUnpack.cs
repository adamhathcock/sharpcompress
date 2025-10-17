using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar;

internal interface IRarUnpack
{
    #if NETSTANDARD2_0 || NETFRAMEWORK
    void DoUnpack(FileHeader fileHeader, Stream readStream, Stream writeStream);
    void DoUnpack();
#else
    ValueTask DoUnpackAsync(FileHeader fileHeader, Stream readStream, Stream writeStream);
     ValueTask  DoUnpackAsync();
#endif
    // eg u/i pause/resume button
    bool Suspended { get; set; }

    long DestSize { get; }
    int Char { get; }
    int PpmEscChar { get; set; }
}
