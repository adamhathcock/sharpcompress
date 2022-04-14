using ZstdSharp.Unsafe;

namespace ZstdSharp
{
    public static unsafe class ThrowHelper
    {
        private const ulong ZSTD_CONTENTSIZE_UNKNOWN = unchecked(0UL - 1);
        private const ulong ZSTD_CONTENTSIZE_ERROR = unchecked(0UL - 2);

        public static nuint EnsureZstdSuccess(this nuint returnValue)
        {
            if (Methods.ZSTD_isError(returnValue) != 0)
                ThrowException(returnValue, Methods.ZSTD_getErrorName(returnValue));

            return returnValue;
        }

        public static nuint EnsureZdictSuccess(this nuint returnValue)
        {
            if (Methods.ZDICT_isError(returnValue) != 0)
                ThrowException(returnValue, Methods.ZDICT_getErrorName(returnValue));

            return returnValue;
        }

        public static ulong EnsureContentSizeOk(this ulong returnValue)
        {
            if (returnValue == ZSTD_CONTENTSIZE_UNKNOWN)
                throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Decompressed content size is not specified");

            if (returnValue == ZSTD_CONTENTSIZE_ERROR)
                throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Decompressed content size cannot be determined (e.g. invalid magic number, srcSize too small)");

            return returnValue;
        }

        private static void ThrowException(nuint returnValue, string message)
        {
            var code = 0 - returnValue;
            throw new ZstdException((ZSTD_ErrorCode) code, message);
        }
    }
}
