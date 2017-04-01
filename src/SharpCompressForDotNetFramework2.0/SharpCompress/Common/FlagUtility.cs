using System;

namespace SharpCompress.Common
{
    internal static class FlagUtility
    {
        /// <summary>
        /// Returns true if the flag is set on the specified bit field.
        /// Currently only works with 32-bit bitfields. 
        /// </summary>
        /// <typeparam name="T">Enumeration with Flags attribute</typeparam>
        /// <param name="bitField">Flagged variable</param>
        /// <param name="flag">Flag to test</param>
        /// <returns></returns>
        public static bool HasFlag<T>(long bitField, T flag)
            where T : struct, IConvertible
        {
            return HasFlag(bitField, flag.ToInt64(null));
        }

        /// <summary>
        /// Returns true if the flag is set on the specified bit field.
        /// Currently only works with 32-bit bitfields. 
        /// </summary>
        /// <typeparam name="T">Enumeration with Flags attribute</typeparam>
        /// <param name="bitField">Flagged variable</param>
        /// <param name="flag">Flag to test</param>
        /// <returns></returns>
        public static bool HasFlag<T>(ulong bitField, T flag)
            where T : struct, IConvertible
        {
            return HasFlag(bitField, flag.ToUInt64(null));
        }

        /// <summary>
        /// Returns true if the flag is set on the specified bit field.
        /// Currently only works with 32-bit bitfields. 
        /// </summary>
        /// <param name="bitField">Flagged variable</param>
        /// <param name="flag">Flag to test</param>
        /// <returns></returns>
        public static bool HasFlag(ulong bitField, ulong flag)
        {
            return ((bitField & flag) == flag);
        }

        public static bool HasFlag(short bitField, short flag)
        {
            return ((bitField & flag) == flag);
        }

#if PORTABLE
        /// <summary>
        /// Generically checks enums in a Windows Phone 7 enivronment
        /// </summary>
        /// <param name="enumVal"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool HasFlag(this Enum enumVal, Enum flag)
        {
            if (enumVal.GetHashCode() > 0) //GetHashCode returns the enum value. But it's something very crazy if not set beforehand
            {
                ulong num = Convert.ToUInt64(flag.GetHashCode());
                return ((Convert.ToUInt64(enumVal.GetHashCode()) & num) == num);
            }
            else
            {
                return false;
            }
        }
#endif

#if THREEFIVE
        /// <summary>
        /// Generically checks enums in a .NET 3.5
        /// </summary>
        /// <param name="enumVal"></param>
        /// <param name="flag"></param>
        /// <returns></returns>
        public static bool HasFlag(this Enum enumVal, Enum flag)
        {
            return (Convert.ToInt32(enumVal) & Convert.ToInt32(flag)) == Convert.ToInt32(flag);
        }
#endif

        /// <summary>
        /// Returns true if the flag is set on the specified bit field.
        /// Currently only works with 32-bit bitfields. 
        /// </summary>
        /// <typeparam name="T">Enumeration with Flags attribute</typeparam>
        /// <param name="bitField">Flagged variable</param>
        /// <param name="flag">Flag to test</param>
        /// <returns></returns>
        public static bool HasFlag<T>(T bitField, T flag)
            where T : struct
        {
            return HasFlag(Convert.ToInt64(bitField), Convert.ToInt64(flag));
        }

        /// <summary>
        /// Returns true if the flag is set on the specified bit field.
        /// Currently only works with 32-bit bitfields. 
        /// </summary>
        /// <param name="bitField">Flagged variable</param>
        /// <param name="flag">Flag to test</param>
        /// <returns></returns>
        public static bool HasFlag(long bitField, long flag)
        {
            return ((bitField & flag) == flag);
        }


        /// <summary>
        /// Sets a bit-field to either on or off for the specified flag.
        /// </summary>
        /// <typeparam name="T">Enumeration with Flags attribute</typeparam>
        /// <param name="bitField">Flagged variable</param>
        /// <param name="flag">Flag to change</param>
        /// <param name="on">bool</param>
        /// <returns>The flagged variable with the flag changed</returns>
        public static long SetFlag<T>(long bitField, T flag, bool @on)
            where T : struct, IConvertible
        {
            if (@on)
            {
                return bitField | flag.ToInt64(null);
            }
            else
            {
                return bitField & (~flag.ToInt64(null));
            }
        }

        /// <summary>
        /// Sets a bit-field to either on or off for the specified flag.
        /// </summary>
        /// <typeparam name="T">Enumeration with Flags attribute</typeparam>
        /// <param name="bitField">Flagged variable</param>
        /// <param name="flag">Flag to change</param>
        /// <param name="on">bool</param>
        /// <returns>The flagged variable with the flag changed</returns>
        public static long SetFlag<T>(T bitField, T flag, bool @on)
            where T : struct, IConvertible
        {
            return SetFlag(bitField.ToInt64(null), flag, @on);
        }
    }
}
