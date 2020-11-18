#if NET461 || NETSTANDARD2_0

namespace System
{
    public static class StringExtensions
    {
        public static bool EndsWith(this string text, char value)
        {
            return text.Length > 0 && text[text.Length - 1] == value;
        }

        public static bool Contains(this string text, char value)
        {
            return text.IndexOf(value) > -1;
        }
    }
}

#endif