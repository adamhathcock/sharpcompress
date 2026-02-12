using System.IO;
using SharpCompress.Archives.GZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;

namespace SharpCompress.Readers.Tar;

public partial class TarReader
#if NET8_0_OR_GREATER
    : IReaderOpenable
#endif
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

    /// <summary>
    /// Opens a TarReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        options ??= new ReaderOptions();
        var sharpCompressStream = SharpCompressStream.Create(
            stream,
            bufferSize: options.RewindableBufferSize
        );
        long pos = sharpCompressStream.Position;
        if (GZipArchive.IsGZipFile(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = new GZipStream(sharpCompressStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.GZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        if (BZip2Stream.IsBZip2(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = BZip2Stream.Create(
                sharpCompressStream,
                CompressionMode.Decompress,
                false
            );
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.BZip2);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        if (ZStandardStream.IsZStandard(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = new ZStandardStream(sharpCompressStream);
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.ZStandard);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        if (LZipStream.IsLZipFile(sharpCompressStream))
        {
            sharpCompressStream.Position = pos;
            var testStream = new LZipStream(sharpCompressStream, CompressionMode.Decompress);
            if (TarArchive.IsTarFile(testStream))
            {
                sharpCompressStream.Position = pos;
                return new TarReader(sharpCompressStream, options, CompressionType.LZip);
            }
            throw new InvalidFormatException("Not a tar file.");
        }
        sharpCompressStream.Position = pos;
        return new TarReader(sharpCompressStream, options, CompressionType.None);
    }
}
