using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpCompress.Compressors.LZMA;

internal static class Log
{
    private static readonly Stack<string> INDENT = new();
    private static bool NEEDS_INDENT = true;

    static Log() => INDENT.Push("");

    public static void PushIndent(string indent = "  ") => INDENT.Push(INDENT.Peek() + indent);

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
#if DEBUG_LZMA
            Debug.Write(INDENT.Peek());
#endif
        }
    }

    public static void Write(object value)
    {
        EnsureIndent();
#if DEBUG_LZMA
        Debug.Write(value);
#endif
    }

    public static void Write(string text)
    {
        EnsureIndent();
#if DEBUG_LZMA
        Debug.Write(text);
#endif
    }

    public static void Write(string format, params object[] args)
    {
        EnsureIndent();
#if DEBUG_LZMA
        Debug.Write(string.Format(format, args));
#endif
    }

    public static void WriteLine()
    {
#if DEBUG_LZMA
        Debug.WriteLine("");
#endif
        NEEDS_INDENT = true;
    }

    public static void WriteLine(object value)
    {
        EnsureIndent();
#if DEBUG_LZMA
        Debug.WriteLine(value);
#endif
        NEEDS_INDENT = true;
    }

    public static void WriteLine(string text)
    {
        EnsureIndent();
#if DEBUG_LZMA
        Debug.WriteLine(text);
#endif
        NEEDS_INDENT = true;
    }

    public static void WriteLine(string format, params object[] args)
    {
        EnsureIndent();
#if DEBUG_LZMA
        Debug.WriteLine(string.Format(format, args));
#endif
        NEEDS_INDENT = true;
    }
}
