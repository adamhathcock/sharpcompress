#if NET8_0_OR_GREATER
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Writers.Tar;

public partial class TarWriter : IWriterOpenable<ITarWriter, ITarAsyncWriter, TarWriterOptions>
{
    public static ITarWriter OpenWriter(string filePath, TarWriterOptions writerOptions)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenWriter(new FileInfo(filePath), writerOptions);
    }

    public static ITarWriter OpenWriter(FileInfo fileInfo, TarWriterOptions writerOptions)
    {
        fileInfo.NotNull(nameof(fileInfo));
        return new TarWriter(fileInfo.OpenWrite(), writerOptions);
    }

    public static ITarWriter OpenWriter(Stream stream, TarWriterOptions writerOptions)
    {
        stream.NotNull(nameof(stream));
        return new TarWriter(stream, writerOptions);
    }

    public static ITarAsyncWriter OpenAsyncWriter(string filePath, TarWriterOptions writerOptions)
    {
        return (ITarAsyncWriter)OpenWriter(filePath, writerOptions);
    }

    public static ITarAsyncWriter OpenAsyncWriter(Stream stream, TarWriterOptions writerOptions)
    {
        return (ITarAsyncWriter)OpenWriter(stream, writerOptions);
    }

    public static ITarAsyncWriter OpenAsyncWriter(FileInfo fileInfo, TarWriterOptions writerOptions)
    {
        return (ITarAsyncWriter)OpenWriter(fileInfo, writerOptions);
    }
}
#endif
