using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers.Zip;
using Xunit;

namespace SharpCompress.Test.Zip;

public class Zip64DescriptorSizeTests
{
    public static IEnumerable<object[]> Sizes()
    {
        foreach (var useAsync in new[] { false, true })
        {
            foreach (var signature in new[] { false, true })
            {
                foreach (
                    var size in new[]
                    {
                        0UL,
                        1UL,
                        0xFFFFFFFEUL,
                        0xFFFFFFFFUL,
                        0x100000000UL,
                        0x10000007BUL,
                        0x123456789UL,
                    }
                )
                {
                    yield return new object[] { useAsync, signature, size };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task Descriptor_Preserves_Uncompressed_Size_And_Next_Header(
        bool useAsync,
        bool signature,
        ulong size
    )
    {
        using var output = new MemoryStream();
        using (
            var writer = new ZipWriter(
                new ForwardOnlyStream(output),
                new ZipWriterOptions(CompressionType.Deflate) { UseZip64 = true }
            )
        )
        {
            using var first = new MemoryStream([42]);
            writer.Write("first", first, DateTime.Now);
            using var second = new MemoryStream([43]);
            writer.Write("second", second, DateTime.Now);
        }
        var zip = output.ToArray();
        var descriptor = Enumerable
            .Range(0, zip.Length - 24)
            .First(i => BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(i)) == 0x08074b50);
        // Parser-only metadata probe: advertise a large size without allocating a large payload.
        // The Archive API is intentionally not used: this synthetic descriptor differs from the directory.
        BinaryPrimitives.WriteUInt64LittleEndian(zip.AsSpan(descriptor + 16), size);
        if (!signature)
        {
            zip = zip.Take(descriptor).Concat(zip.Skip(descriptor + 4)).ToArray();
        }
        using var input = new ForwardOnlyStream(new MemoryStream(zip));
        if (useAsync)
        {
            await using var reader = await ReaderFactory.OpenAsyncReader(
                new AsyncOnlyStream(input),
                ReaderOptions.ForExternalStream,
                CancellationToken.None
            );
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            var first = reader.Entry;
#if LEGACY_DOTNET
            using (var stream = await reader.OpenEntryStreamAsync(CancellationToken.None))
#else
            await using (var stream = await reader.OpenEntryStreamAsync(CancellationToken.None))
#endif
            {
                await stream.CopyToAsync(Stream.Null, 81920, CancellationToken.None);
            }
            Assert.True(await reader.MoveToNextEntryAsync(CancellationToken.None));
            Assert.Equal((long)size, first.Size);
            Assert.Equal("second", reader.Entry.Key);
        }
        else
        {
            using var reader = ReaderFactory.OpenReader(input, ReaderOptions.ForExternalStream);
            Assert.True(reader.MoveToNextEntry());
            var first = reader.Entry;
            using (var stream = reader.OpenEntryStream())
            {
                stream.CopyTo(Stream.Null);
            }
            Assert.True(reader.MoveToNextEntry());
            Assert.Equal((long)size, first.Size);
            Assert.Equal("second", reader.Entry.Key);
        }
    }
}
