using System;
using System.Collections.Generic;
using System.IO;

namespace SharpCompress;

internal static class StreamValidationExtensions
{
    internal static void RequireReadable(this Stream stream)
    {
        stream.NotNull(nameof(stream));

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable", nameof(stream));
        }
    }

    internal static void RequireSeekable(this Stream stream)
    {
        stream.NotNull(nameof(stream));

        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }
    }

    internal static void RequireWritable(this Stream stream)
    {
        stream.NotNull(nameof(stream));

        if (!stream.CanWrite)
        {
            throw new ArgumentException("Stream must be writable", nameof(stream));
        }
    }

    internal static IEnumerable<Stream> RequireSeekable(this IEnumerable<Stream> streams)
    {
        foreach (var stream in streams)
        {
            stream.RequireSeekable();
            yield return stream;
        }
    }

    internal static IEnumerable<Stream> RequireReadable(this IEnumerable<Stream> streams)
    {
        foreach (var stream in streams)
        {
            stream.RequireReadable();
            yield return stream;
        }
    }
}
