using System.IO;

namespace SharpCompress.Readers;

public static partial class ReaderFactory
{
    public static IReader OpenReader(string filePath, ReaderOptions? options = null)
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return OpenReader(new FileInfo(filePath), options ?? ReaderOptions.ForFilePath);
    }

    public static IReader OpenReader(FileInfo fileInfo, ReaderOptions? options = null)
    {
        options ??= ReaderOptions.ForFilePath;
        return OpenReader(fileInfo.OpenRead(), options);
    }

    /// <summary>
    /// Opens a Reader for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null)
    {
        stream.RequireReadable();
        options ??= ReaderOptions.ForExternalStream;

        return ReaderFormatDetection.OpenReader(stream, options);
    }
}
