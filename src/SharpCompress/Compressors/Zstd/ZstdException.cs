using System;
using ZstdSharp.Unsafe;

namespace ZstdSharp
{
    public class ZstdException : Exception
    {
        public ZstdException(ZSTD_ErrorCode code, string message) : base(message)
            => Code = code;

        public ZSTD_ErrorCode Code { get; }
    }
}
