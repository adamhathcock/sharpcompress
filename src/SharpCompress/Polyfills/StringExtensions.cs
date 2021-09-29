#if NETFRAMEWORK || NETSTANDARD2_0

namespace SharpCompress
{
    internal static class StringExtensions
    {
        internal static bool EndsWith(this string text, char value)
        {
            return text.Length > 0 && text[text.Length - 1] == value;
        }

        internal static bool Contains(this string text, char value)
        {
            return text.IndexOf(value) > -1;
        }
    }
}

#endif