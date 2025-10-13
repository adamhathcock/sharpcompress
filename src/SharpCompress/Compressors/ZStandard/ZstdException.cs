using System;
using SharpCompress.Common;
using ZstdSharp.Unsafe;

namespace ZstdSharp
{


    public class ZstdException : SharpCompressException
    {
        public ZstdException(ZSTD_ErrorCode code, string message) : base(message)
            => Code = code;

        public ZSTD_ErrorCode Code { get; }
    }
}
