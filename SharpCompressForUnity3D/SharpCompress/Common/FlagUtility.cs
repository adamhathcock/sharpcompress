namespace SharpCompress.Common
{
    using System;

    internal static class FlagUtility
    {
        public static bool HasFlag(short bitField, short flag)
        {
            return ((bitField & flag) == flag);
        }

        public static bool HasFlag(long bitField, long flag)
        {
            return ((bitField & flag) == flag);
        }

        public static bool HasFlag<T>(long bitField, T flag) where T: struct
        {
            return HasFlag<T>(bitField, flag);
        }

        public static bool HasFlag(ulong bitField, ulong flag)
        {
            return ((bitField & flag) == flag);
        }

        public static bool HasFlag<T>(ulong bitField, T flag) where T: struct
        {
            return HasFlag<T>(bitField, flag);
        }

        public static bool HasFlag<T>(T bitField, T flag) where T: struct
        {
            return HasFlag(Convert.ToInt64(bitField), Convert.ToInt64(flag));
        }

        public static long SetFlag(long bitField, long flag, bool on)
        {
            if (on)
            {
                return (bitField | flag);
            }
            return (bitField & ~flag);
        }

        public static long SetFlag<T>(T bitField, T flag, bool on) where T: struct
        {
            return SetFlag(Convert.ToInt64(bitField), Convert.ToInt64(flag), on);
        }
    }
}

