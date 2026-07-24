using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;
using SharpCompress.Readers.Tar;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Tar;
using Xunit;

namespace SharpCompress.Test;

public class ReaderFactoryTests : TestBase
{
    [Fact]
    public void OpenReader_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        Assert.Throws<ArgumentException>(() => ReaderFactory.OpenReader(unreadable));
    }

    [Fact]
    public async ValueTask OpenAsyncReader_Stream_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ReaderFactory.OpenAsyncReader(unreadable).AsTask()
        );
    }

    [Fact]
    public void RarReader_StreamCollection_Throws_On_Unreadable_Stream()
    {
        using var unreadable = new TestStream(new MemoryStream(), false, true, true);
        using var readable = new MemoryStream();

        Assert.Throws<ArgumentException>(() =>
            RarReader.OpenReader([unreadable, readable]).MoveToNextEntry()
        );
    }

    [Fact]
    public void OpenReader_DeflateStream_WithTarPayload_DetectsTarReader()
    {
        using var compressedStream = new MemoryStream(CompressWithDeflate(CreateTarPayload()));
        using var sharpCompressStream = SharpCompress.IO.SharpCompressStream.CreateNonDisposing(
            compressedStream
        );
        using var deflateStream = new DeflateStream(
            sharpCompressStream,
            CompressionMode.Decompress,
            CompressionLevel.Default,
            leaveOpen: false
        );
        using var reader = ReaderFactory.OpenReader(deflateStream);

        Assert.IsType<TarReader>(reader);
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["alpha.txt"] = "alpha",
                ["nested/beta.txt"] = "beta",
            },
            ReadAllFiles(reader)
        );
    }

    private static Dictionary<string, string> ReadAllFiles(IReader reader)
    {
        var entries = new Dictionary<string, string>();
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            using var entryStream = reader.OpenEntryStream();
            using var streamReader = new StreamReader(entryStream, Encoding.UTF8);
            entries.Add(reader.Entry.Key!, streamReader.ReadToEnd());
        }

        return entries;
    }

    private static byte[] CreateTarPayload()
    {
        var archiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 };
        var tarWriterOptions = new TarWriterOptions(CompressionType.None, true)
        {
            ArchiveEncoding = archiveEncoding,
        };

        using var memoryStream = new MemoryStream();
        using (var tarWriter = new TarWriter(memoryStream, tarWriterOptions))
        using (var alphaStream = new MemoryStream(Encoding.UTF8.GetBytes("alpha")))
        using (var betaStream = new MemoryStream(Encoding.UTF8.GetBytes("beta")))
        {
            tarWriter.Write("alpha.txt", alphaStream, null);
            tarWriter.Write("nested/beta.txt", betaStream, null);
        }

        return memoryStream.ToArray();
    }

    private static byte[] CompressWithDeflate(byte[] bytes)
    {
        using var sourceStream = new MemoryStream(bytes);
        using var deflateStream = new DeflateStream(
            sourceStream,
            CompressionMode.Compress,
            CompressionLevel.Default
        );
        using var compressedStream = new MemoryStream();

        var buffer = new byte[4096];
        int read;
        while ((read = deflateStream.Read(buffer, 0, buffer.Length)) > 0)
        {
            compressedStream.Write(buffer, 0, read);
        }

        return compressedStream.ToArray();
    }
}
