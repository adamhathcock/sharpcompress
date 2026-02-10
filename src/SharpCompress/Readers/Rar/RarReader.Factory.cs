#if NET8_0_OR_GREATER
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Readers.Rar;

public partial class RarReader : IReaderOpenable
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
}
#endif
