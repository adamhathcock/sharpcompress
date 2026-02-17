#if NET8_0_OR_GREATER
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Writers.Zip;

public partial class ZipWriter : IWriterOpenable<IZipWriter, IZipAsyncWriter, ZipWriterOptions>
{
    public static IZipWriter OpenWriter(string filePath, ZipWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    public static IZipWriter OpenWriter(FileInfo fileInfo, ZipWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new ZipWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static IZipWriter OpenWriter(Stream stream, ZipWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new ZipWriter(stream, writerOptions);
    }

    public static IZipAsyncWriter OpenAsyncWriter(string filePath, ZipWriterOptions writerOptions)
    {
        return (IZipAsyncWriter)OpenWriter(filePath, writerOptions);
    }

    public static IZipAsyncWriter OpenAsyncWriter(Stream stream, ZipWriterOptions writerOptions)
    {
        return (IZipAsyncWriter)OpenWriter(stream, writerOptions);
    }

    public static IZipAsyncWriter OpenAsyncWriter(FileInfo fileInfo, ZipWriterOptions writerOptions)
    {
        return (IZipAsyncWriter)OpenWriter(fileInfo, writerOptions);
    }
}
#endif
