using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Common.SevenZip;
using SharpCompress.Crypto;
using SharpCompress.Writers.SevenZip;
using Xunit;

namespace SharpCompress.Test.SevenZip;

public class SevenZipEncryptionDetectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SevenZipArchive_EmptyFirstFile_EncryptionDetection(bool encrypted)
    {
        using var output = new MemoryStream();
        using (var writer = new SevenZipWriter(output, new SevenZipWriterOptions()))
        {
            using var empty = new MemoryStream();
            using var data = new MemoryStream(new byte[] { 1, 2, 3 });
            writer.Write("empty.txt", empty, null);
            writer.Write("data.txt", data, null);
        }
        output.Position = 0;
        using var archive = (SevenZipArchive)SevenZipArchive.OpenArchive(output);
        var entries = archive.Entries.Cast<SevenZipArchiveEntry>().ToList();
        if (encrypted)
        {
            // Only metadata is under test; do not decode after adding the AES marker.
            entries[1]
                .FilePart.Folder!._coders.Add(new CCoderInfo { _methodId = CMethodId.K_AES });
        }
        Assert.False(entries[0].IsEncrypted);
        Assert.Same(Stream.Null, entries[0].OpenEntryStream());
        Assert.Equal(encrypted, entries[1].IsEncrypted);
        Assert.Equal(encrypted, archive.IsEncrypted);
        Assert.Equal(encrypted, await archive.IsEncryptedAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SevenZipArchive_NoFiles_IsNotEncrypted(bool directory)
    {
        using var stream = new MemoryStream();
        // Build reader fixtures directly so this PR does not depend on the writer fix in #1440.
        using var header = new MemoryStream();
        ArchiveHeaderWriter.WriteRawHeader(
            header,
            null,
            new SevenZipFilesInfoWriter
            {
                Entries = directory
                    ? new[]
                    {
                        new SevenZipWriteEntry
                        {
                            Name = "dir",
                            IsDirectory = true,
                            IsEmpty = true,
                            Attributes = 0x10,
                        },
                    }
                    : System.Array.Empty<SevenZipWriteEntry>(),
            }
        );
        var bytes = header.ToArray();
        var crc = Crc32Stream.Compute(
            Crc32Stream.DEFAULT_POLYNOMIAL,
            Crc32Stream.DEFAULT_SEED,
            bytes
        );
        SevenZipSignatureHeaderWriter.WriteFinal(stream, 0, (ulong)bytes.Length, crc);
        stream.Write(bytes, 0, bytes.Length);
        stream.Position = 0;
        using var archive = SevenZipArchive.OpenArchive(stream);
        Assert.False(archive.IsEncrypted);
    }
}
