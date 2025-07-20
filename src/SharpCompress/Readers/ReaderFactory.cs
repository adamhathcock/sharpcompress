using System.IO;
using System.Linq;
using SharpCompress.Common;
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

        var bStream = new SharpCompressStream(stream, bufferSize: options.BufferSize);

        foreach (var factory in Factories.Factory.Factories.OfType<Factories.Factory>())
        {
            ((IStreamStack)bStream).StackSeek(0);
            if (factory.TryOpenReader(bStream, options, out var reader) && reader != null)
            {
                return reader;
            }
        }

        throw new InvalidFormatException(
            "Cannot determine compressed stream type.  Supported Reader Formats: Arc, Zip, GZip, BZip2, Tar, Rar, LZip, XZ"
        );
    }
}
