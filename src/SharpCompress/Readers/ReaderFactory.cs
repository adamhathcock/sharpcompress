using System;
using System.IO;
using System.Linq;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public static class ReaderFactory
{
    /// <summary>
    /// Opens a Reader for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader Open(Stream stream, ReaderOptions? options = null)
    {
        stream.CheckNotNull(nameof(stream));
        options ??= new ReaderOptions() { LeaveStreamOpen = false };

        var rewindableStream = new RewindableStream(stream);
        rewindableStream.StartRecording();

        foreach (var factory in Factories.Factory.Factories.OfType<Factories.Factory>())
        {
            if (factory.TryOpenReader(rewindableStream, options, out var reader) && reader != null)
            {
                return reader;
            }
        }

        throw new InvalidOperationException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Zip, GZip, BZip2, Tar, Rar, LZip, XZ"
        );
    }
}
