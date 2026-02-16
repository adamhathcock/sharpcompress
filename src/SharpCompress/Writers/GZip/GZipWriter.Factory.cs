#if NET8_0_OR_GREATER
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Writers.GZip;

public partial class GZipWriter : IWriterOpenable<GZipWriterOptions>
{
    public static IWriter OpenWriter(string filePath, GZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    public static IWriter OpenWriter(FileInfo fileInfo, GZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new GZipWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IWriter OpenWriter(Stream stream, GZipWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new GZipWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(string stream, GZipWriterOptions writerOptions)
    {
        return (IAsyncWriter)OpenWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(Stream stream, GZipWriterOptions writerOptions)
    {
        return (IAsyncWriter)OpenWriter(stream, writerOptions);
    }

    public static IAsyncWriter OpenAsyncWriter(FileInfo fileInfo, GZipWriterOptions writerOptions)
    {
        return (IAsyncWriter)OpenWriter(fileInfo, writerOptions);
    }
}
#endif
