using SharpCompress.Common;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

public class ZstdException : SharpCompressException
{
    public ZstdException(ZSTD_ErrorCode code, string message)
        : base(message) => Code = code;

    public ZSTD_ErrorCode Code { get; }
}
