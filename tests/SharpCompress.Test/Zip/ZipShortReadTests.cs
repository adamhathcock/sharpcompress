using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Test.Mocks;
using SharpCompress.Writers;
using Xunit;

namespace SharpCompress.Test.Zip;

/// <summary>
/// Tests for ZIP reading with streams that return short reads.
/// Reproduces the regression where ZIP parsing fails depending on Stream.Read chunking patterns.
/// </summary>
public class ZipShortReadTests : ReaderTests
{
    /// <summary>
    /// A non-seekable stream that returns controlled short reads.
    /// Simulates real-world network/multipart streams that legally return fewer bytes than requested.
    /// </summary>
    private sealed class PatternReadStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly int _firstReadSize;
        private readonly int _chunkSize;
        private bool _firstReadDone;

        public PatternReadStream(byte[] bytes, int firstReadSize, int chunkSize)
        {
            _inner = new MemoryStream(bytes, writable: false);
            _firstReadSize = firstReadSize;
            _chunkSize = chunkSize;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int limit = !_firstReadDone ? _firstReadSize : _chunkSize;
            _firstReadDone = true;

            int toRead = Math.Min(count, limit);
            return _inner.Read(buffer, offset, toRead);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// Test that ZIP reading works correctly with short reads on non-seekable streams.
    /// Uses a test archive and different chunking patterns.
    /// </summary>
    [Theory]
    [InlineData("Zip.deflate.zip", 1000, 4096)]
    [InlineData("Zip.deflate.zip", 999, 4096)]
    [InlineData("Zip.deflate.zip", 100, 4096)]
    [InlineData("Zip.deflate.zip", 50, 512)]
    [InlineData("Zip.deflate.zip", 1, 1)]  // Extreme case: 1 byte at a time
    [InlineData("Zip.deflate.dd.zip", 1000, 4096)]
    [InlineData("Zip.deflate.dd.zip", 999, 4096)]
    [InlineData("Zip.zip64.zip", 3816, 4096)]
    [InlineData("Zip.zip64.zip", 3815, 4096)]  // Similar to the issue pattern
    public void Zip_Reader_Handles_Short_Reads(string zipFile, int firstReadSize, int chunkSize)
    {
        // Use an existing test ZIP file
        var zipPath = Path.Combine(TEST_ARCHIVES_PATH, zipFile);
        if (!File.Exists(zipPath))
        {
            return; // Skip if file doesn't exist
        }
        
        var bytes = File.ReadAllBytes(zipPath);

        // Baseline with MemoryStream (seekable, no short reads)
        var baseline = ReadEntriesFromStream(new MemoryStream(bytes, writable: false));
        Assert.NotEmpty(baseline);

        // Non-seekable stream with controlled short read pattern
        var chunked = ReadEntriesFromStream(new PatternReadStream(bytes, firstReadSize, chunkSize));
        Assert.Equal(baseline, chunked);
    }

    private List<string> ReadEntriesFromStream(Stream stream)
    {
        var names = new List<string>();
        using var reader = ReaderFactory.Open(stream, new ReaderOptions { LeaveStreamOpen = true });

        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                continue;
            }

            names.Add(reader.Entry.Key!);

            using var entryStream = reader.OpenEntryStream();
            entryStream.CopyTo(Stream.Null);
        }

        return names;
    }
}
