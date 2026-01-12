using System.IO;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace SharpCompress.Archives;

public static class IWritableArchiveExtensions
{
    public static void SaveTo(
        this IWritableArchive writableArchive,
        string filePath,
        WriterOptions? options = null
    ) => writableArchive.SaveTo(new FileInfo(filePath), options ?? new(CompressionType.Deflate));

    public static void SaveTo(
        this IWritableArchive writableArchive,
        FileInfo fileInfo,
        WriterOptions? options = null
    )
    {
        using var stream = fileInfo.Open(FileMode.Create, FileAccess.Write);
        writableArchive.SaveTo(stream, options ?? new(CompressionType.Deflate));
    }
}
