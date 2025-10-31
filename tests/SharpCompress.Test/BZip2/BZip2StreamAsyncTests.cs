using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Compressors.BZip2;
using Xunit;

namespace SharpCompress.Test.BZip2;

public class BZip2StreamAsyncTests
{
    private byte[] CreateTestData(int size)
    {
        var data = new byte[size];
        // Create compressible data with repetitive pattern
        for (int i = 0; i < size; i++)
        {
            data[i] = (byte)('A' + (i % 26));
        }
        return data;
    }

    [Fact]
    public async Task BZip2CompressDecompressAsyncTest()
    {
        var testData = CreateTestData(10000);
        byte[] compressed;

        // Compress
        using (var memoryStream = new MemoryStream())
        {
            using (
                var bzip2Stream = new BZip2Stream(
                    memoryStream,
                    SharpCompress.Compressors.CompressionMode.Compress,
                    false
                )
            )
            {
                await bzip2Stream.WriteAsync(testData, 0, testData.Length);
                (bzip2Stream as BZip2Stream)?.Finish();
            }
            compressed = memoryStream.ToArray();
        }

        // Verify compression occurred
        Assert.True(compressed.Length > 0);
        Assert.True(compressed.Length < testData.Length);

        // Decompress
        byte[] decompressed;
        using (var memoryStream = new MemoryStream(compressed))
        {
            using (
                var bzip2Stream = new BZip2Stream(
                    memoryStream,
                    SharpCompress.Compressors.CompressionMode.Decompress,
                    false
                )
            )
            {
                decompressed = new byte[testData.Length];
                var totalRead = 0;
                int bytesRead;
                while (
                    (
                        bytesRead = await bzip2Stream.ReadAsync(
                            decompressed,
                            totalRead,
                            testData.Length - totalRead
                        )
                    ) > 0
                )
                {
                    totalRead += bytesRead;
                }
            }
        }

        // Verify decompression
        Assert.Equal(testData, decompressed);
    }

    [Fact]
    public async Task BZip2ReadAsyncWithCancellationTest()
    {
        var testData = Encoding.ASCII.GetBytes(new string('A', 5000)); // Repetitive data compresses well
        byte[] compressed;

        // Compress
        using (var memoryStream = new MemoryStream())
        {
            using (
                var bzip2Stream = new BZip2Stream(
                    memoryStream,
                    SharpCompress.Compressors.CompressionMode.Compress,
                    false
                )
            )
            {
                await bzip2Stream.WriteAsync(testData, 0, testData.Length);
                (bzip2Stream as BZip2Stream)?.Finish();
            }
            compressed = memoryStream.ToArray();
        }

        // Decompress with cancellation support
        using (var memoryStream = new MemoryStream(compressed))
        {
            using (
                var bzip2Stream = new BZip2Stream(
                    memoryStream,
                    SharpCompress.Compressors.CompressionMode.Decompress,
                    false
                )
            )
            {
                var buffer = new byte[1024];
                using var cts = new System.Threading.CancellationTokenSource();

                // Read should complete without cancellation
                var bytesRead = await bzip2Stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
                Assert.True(bytesRead > 0);
            }
        }
    }

    [Fact]
    public async Task BZip2MultipleAsyncWritesTest()
    {
        using (var memoryStream = new MemoryStream())
        {
            using (
                var bzip2Stream = new BZip2Stream(
                    memoryStream,
                    SharpCompress.Compressors.CompressionMode.Compress,
                    false
                )
            )
            {
                var data1 = Encoding.ASCII.GetBytes("Hello ");
                var data2 = Encoding.ASCII.GetBytes("World");
                var data3 = Encoding.ASCII.GetBytes("!");

                await bzip2Stream.WriteAsync(data1, 0, data1.Length);
                await bzip2Stream.WriteAsync(data2, 0, data2.Length);
                await bzip2Stream.WriteAsync(data3, 0, data3.Length);

                (bzip2Stream as BZip2Stream)?.Finish();
            }

            var compressed = memoryStream.ToArray();
            Assert.True(compressed.Length > 0);

            // Decompress and verify
            using (var readStream = new MemoryStream(compressed))
            {
                using (
                    var bzip2Stream = new BZip2Stream(
                        readStream,
                        SharpCompress.Compressors.CompressionMode.Decompress,
                        false
                    )
                )
                {
                    var result = new StringBuilder();
                    var buffer = new byte[256];
                    int bytesRead;
                    while ((bytesRead = await bzip2Stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        result.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                    }

                    Assert.Equal("Hello World!", result.ToString());
                }
            }
        }
    }

    [Fact]
    public async Task BZip2LargeDataAsyncTest()
    {
        var largeData = CreateTestData(100000);

        // Compress
        byte[] compressed;
        using (var memoryStream = new MemoryStream())
        {
            using (
                var bzip2Stream = new BZip2Stream(
                    memoryStream,
                    SharpCompress.Compressors.CompressionMode.Compress,
                    false
                )
            )
            {
                await bzip2Stream.WriteAsync(largeData, 0, largeData.Length);
                (bzip2Stream as BZip2Stream)?.Finish();
            }
            compressed = memoryStream.ToArray();
        }

        // Decompress
        byte[] decompressed;
        using (var memoryStream = new MemoryStream(compressed))
        {
            using (
                var bzip2Stream = new BZip2Stream(
                    memoryStream,
                    SharpCompress.Compressors.CompressionMode.Decompress,
                    false
                )
            )
            {
                decompressed = new byte[largeData.Length];
                var totalRead = 0;
                int bytesRead;
                var buffer = new byte[4096];
                while ((bytesRead = await bzip2Stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    Array.Copy(buffer, 0, decompressed, totalRead, bytesRead);
                    totalRead += bytesRead;
                }
            }
        }

        // Verify
        Assert.Equal(largeData, decompressed);
    }
}
