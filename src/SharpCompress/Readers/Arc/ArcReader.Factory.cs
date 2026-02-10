#if NET8_0_OR_GREATER
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Readers.Arc;

public partial class ArcReader : IReaderOpenable
{
    public static IAsyncReader OpenAsyncReader(string path, ReaderOptions? readerOptions = null)
    {
        path.NotNullOrEmpty(nameof(path));
        return (IAsyncReader)OpenReader(new FileInfo(path), readerOptions);
    }

    public static IAsyncReader OpenAsyncReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        return (IAsyncReader)OpenReader(stream, readerOptions);
    }

    public static IAsyncReader OpenAsyncReader(
        FileInfo fileInfo,
        ReaderOptions? readerOptions = null
    )
    {
        return (IAsyncReader)OpenReader(fileInfo, readerOptions);
    }

    public static IReader OpenReader(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), readerOptions);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return OpenReader(fileInfo.OpenRead(), readerOptions);
    }
}
#endif
