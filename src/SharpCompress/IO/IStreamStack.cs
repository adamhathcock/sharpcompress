using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SharpCompress.IO;

public interface IStreamStack
{
    /// <summary>
    /// Returns the immediate underlying stream in the stack.
    /// </summary>
    Stream BaseStream();
}

public static class StreamStackExtensions
{
    public static T? GetStream<T>(this IStreamStack stack)
        where T : Stream
    {
        var baseStream = stack.BaseStream();
        if (baseStream is T tStream)
        {
            return tStream;
        }
        else if (baseStream is IStreamStack innerStack)
        {
            return innerStack.GetStream<T>();
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the root underlying stream at the bottom of the stack.
    /// This is useful for seeking when the intermediate streams don't support it.
    /// </summary>
    public static Stream GetRootStream(this IStreamStack stack)
    {
        var current = stack.BaseStream();
        while (current is IStreamStack streamStack)
        {
            current = streamStack.BaseStream();
        }
        return current;
    }

    internal static void Rewind(this IStreamStack stream, int count)
    {
        IStreamStack? current = stream;

        while (current != null)
        {
            if (current is SharpCompressStream sharpCompressStream)
            {
                // Try to rewind within the buffer. If the position is outside the buffered
                // region, silently ignore (matching release behavior where streams without
                // buffering simply didn't rewind).
                var targetPosition = sharpCompressStream.Position - count;
                if (targetPosition >= 0)
                {
                    try
                    {
                        sharpCompressStream.Position = targetPosition;
                    }
                    catch (NotSupportedException)
                    {
                        // Cannot seek outside buffered region - silently ignore
                    }
                }
                return;
            }
            current = current.BaseStream() as IStreamStack;
        }
    }
}
