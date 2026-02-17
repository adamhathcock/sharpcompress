#if NET8_0_OR_GREATER
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Writers.GZip;

public partial class GZipWriter : IWriterOpenable<IGZipWriter, IGZipAsyncWriter, GZipWriterOptions>
{
    public static IGZipWriter OpenWriter(string filePath, GZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    public static IGZipWriter OpenWriter(FileInfo fileInfo, GZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new GZipWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IGZipWriter OpenWriter(Stream stream, GZipWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new GZipWriter(stream, writerOptions);
    }

    public static IGZipAsyncWriter OpenAsyncWriter(string filePath, GZipWriterOptions writerOptions)
    {
        return (IGZipAsyncWriter)OpenWriter(filePath, writerOptions);
    }

    public static IGZipAsyncWriter OpenAsyncWriter(Stream stream, GZipWriterOptions writerOptions)
    {
        return (IGZipAsyncWriter)OpenWriter(stream, writerOptions);
    }

    public static IGZipAsyncWriter OpenAsyncWriter(
        FileInfo fileInfo,
        GZipWriterOptions writerOptions
    )
    {
        return (IGZipAsyncWriter)OpenWriter(fileInfo, writerOptions);
    }
}
#endif
