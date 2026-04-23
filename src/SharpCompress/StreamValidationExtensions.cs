using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpCompress;

internal static class StreamValidationExtensions
{
    internal static Stream RequireReadable(this Stream stream)
    {
        stream.NotNull(nameof(stream));

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable", nameof(stream));
        }

        return stream;
    }

    internal static Stream RequireSeekable(this Stream stream)
    {
        stream.NotNull(nameof(stream));

        if (!stream.CanSeek)
        {
            throw new ArgumentException("Stream must be seekable", nameof(stream));
        }

        return stream;
    }

    internal static Stream RequireWritable(this Stream stream)
    {
        stream.NotNull(nameof(stream));

        if (!stream.CanWrite)
        {
            throw new ArgumentException("Stream must be writable", nameof(stream));
        }

        return stream;
    }

    internal static IReadOnlyList<Stream> RequireSeekable(this IReadOnlyList<Stream> streams)
    {
        streams.NotNull(nameof(streams));

        foreach (var stream in streams)
        {
            stream.RequireSeekable();
        }

        return streams;
    }

    internal static IReadOnlyList<Stream> RequireReadable(this IEnumerable<Stream> streams)
    {
        streams.NotNull(nameof(streams));

        var streamArray = streams as IReadOnlyList<Stream> ?? streams.ToArray();
        foreach (var stream in streamArray)
        {
            stream.RequireReadable();
        }

        return streamArray;
    }
}
