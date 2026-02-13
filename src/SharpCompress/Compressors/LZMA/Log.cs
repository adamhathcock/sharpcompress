using System;
using System.Collections.Generic;

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
        }
    }

    public static void Write(object value)
    {
        EnsureIndent();
    }

    public static void Write(string text)
    {
        EnsureIndent();
    }

    public static void Write(string format, params object[] args)
    {
        EnsureIndent();
    }

    public static void WriteLine()
    {
        NEEDS_INDENT = true;
    }

    public static void WriteLine(object value)
    {
        EnsureIndent();
        NEEDS_INDENT = true;
    }

    public static void WriteLine(string text)
    {
        EnsureIndent();
        NEEDS_INDENT = true;
    }

    public static void WriteLine(string format, params object[] args)
    {
        EnsureIndent();
        NEEDS_INDENT = true;
    }
}
