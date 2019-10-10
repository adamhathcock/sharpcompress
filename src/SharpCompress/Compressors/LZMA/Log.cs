using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpCompress.Compressors.LZMA
{
    internal static class Log
    {
        private static readonly Stack<string> INDENT = new Stack<string>();
        private static bool NEEDS_INDENT = true;

        static Log()
        {
            INDENT.Push("");
        }

        public static void PushIndent(string indent = "  ")
        {
            INDENT.Push(INDENT.Peek() + indent);
        }

        public static void PopIndent()
        {
            if (INDENT.Count == 1)
            {
                throw new InvalidOperationException();
            }

            INDENT.Pop();
        }

        private static void EnsureIndent()
        {
            if (NEEDS_INDENT)
            {
                NEEDS_INDENT = false;
                Debug.Write(INDENT.Peek());
            }
        }

        public static void Write(object value)
        {
            EnsureIndent();
            Debug.Write(value);
        }

        public static void Write(string text)
        {
            EnsureIndent();
            Debug.Write(text);
        }

        public static void Write(string format, params object[] args)
        {
            EnsureIndent();
            Debug.Write(string.Format(format, args));
        }

        public static void WriteLine()
        {
            Debug.WriteLine("");
            NEEDS_INDENT = true;
        }

        public static void WriteLine(object value)
        {
            EnsureIndent();
            Debug.WriteLine(value);
            NEEDS_INDENT = true;
        }

        public static void WriteLine(string text)
        {
            EnsureIndent();
            Debug.WriteLine(text);
            NEEDS_INDENT = true;
        }

        public static void WriteLine(string format, params object[] args)
        {
            EnsureIndent();
            Debug.WriteLine(string.Format(format, args));
            NEEDS_INDENT = true;
        }
    }
}