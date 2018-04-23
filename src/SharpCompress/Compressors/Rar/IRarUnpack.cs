using System.IO;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar
{
    interface IRarUnpack
    {
        void DoUnpack(bool isSolid, FileHeader fileHeader, Stream readStream, Stream writeStream);
        void DoUnpack(bool isSolid);

        // eg u/i pause/resume button
        bool Suspended { get; set; }

        long DestSize { get; }
        int Char { get; }
        int PpmEscChar { get; set; }
    }
}
