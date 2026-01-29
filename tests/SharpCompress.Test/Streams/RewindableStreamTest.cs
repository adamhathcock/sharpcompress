using System;
using System.IO;
using System.Linq;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class RewindableStreamTest
{
    [Fact]
    public void TestRewind()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.StartRecording();
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        stream.Rewind(true);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
    }

    [Fact]
    public void TestIncompleteRewind()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.StartRecording();
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        stream.Rewind(true);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
    }

    [Fact]
    public void TestRecording()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(ms);
        stream.StartRecording();
        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        stream.Rewind(false);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
    }

    [Fact]
    public void TestPosition()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        for (int i = 0; i < 10; i++)
        {
            bw.Write(i);
        }
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(new ForwardOnlyStream(ms));
        Assert.Equal(0, stream.Position);

        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);
        Assert.Equal(4, stream.Position);

        stream.StartRecording();
        stream.Read(buffer, 0, 4);
        Assert.Equal(8, stream.Position);

        stream.Rewind();
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public void TestPositionSeek()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        for (int i = 0; i < 10; i++)
        {
            bw.Write(i);
        }
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(new ForwardOnlyStream(ms));
        stream.StartRecording();
        var br = new BinaryReader(stream);

        Assert.Equal(0, br.ReadInt32());
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());

        stream.Position = 4;
        Assert.Equal(1, br.ReadInt32());
    }

    [Fact]
    public void TestDispose()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Flush();
        ms.Position = 0;
        var stream = new RewindableStream(new ForwardOnlyStream(ms));
        stream.Dispose();
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[4], 0, 4));
    }

    [Fact]
    public void TestStopRecordingBasic()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));
        stream.StartRecording();
        var br = new BinaryReader(stream);

        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        stream.StopRecording();

        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());

        Assert.False(stream.IsRecording);
    }

    [Fact]
    public void TestStopRecordingNoFurtherBuffering()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(new ForwardOnlyStream(ms));
        stream.StartRecording();

        var buffer = new byte[8];
        stream.Read(buffer, 0, 8);

        stream.StopRecording();

        stream.Read(buffer, 0, 8);
        Assert.Equal(BitConverter.GetBytes(1), buffer.Take(4).ToArray());
        Assert.Equal(BitConverter.GetBytes(2), buffer.Skip(4).Take(4).ToArray());

        int bytesRead = stream.Read(buffer, 0, 8);
        Assert.Equal(8, bytesRead);

        Assert.False(stream.IsRecording);

        bytesRead = stream.Read(buffer, 0, 8);
        Assert.Equal(0, bytesRead);
    }

#if !LEGACY_DOTNET
    [Fact]
    public void TestStopRecordingWithSpan()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        var buffer = new byte[8];
        stream.Read(buffer);

        stream.StopRecording();

        stream.Read(buffer);
        Assert.Equal(BitConverter.GetBytes(1), buffer.Take(4).ToArray());
        Assert.Equal(BitConverter.GetBytes(2), buffer.Skip(4).Take(4).ToArray());

        int bytesRead = stream.Read(buffer);
        Assert.Equal(8, bytesRead);
        Assert.Equal(BitConverter.GetBytes(3), buffer.Take(4).ToArray());
        Assert.Equal(BitConverter.GetBytes(4), buffer.Skip(4).Take(4).ToArray());
    }
#endif

    [Fact]
    public void TestNonSeekableStream_Rewind()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;

        var nonSeekableStream = new NonSeekableStreamWrapper(ms);
        var stream = new RewindableStream(nonSeekableStream);
        stream.StartRecording();
        var br = new BinaryReader(stream);

        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        stream.Rewind(true);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
    }

    [Fact]
    public void TestNonSeekableStream_Recording()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;

        var nonSeekableStream = new NonSeekableStreamWrapper(ms);
        var stream = new RewindableStream(nonSeekableStream);
        stream.StartRecording();
        var br = new BinaryReader(stream);

        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        stream.Rewind(false);
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
    }

    [Fact]
    public void TestNonSeekableStream_Position()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        for (int i = 0; i < 10; i++)
        {
            bw.Write(i);
        }
        bw.Flush();
        ms.Position = 0;

        var nonSeekableStream = new NonSeekableStreamWrapper(ms);
        var stream = new RewindableStream(nonSeekableStream);
        Assert.Equal(0, stream.Position);
        Assert.True(stream.CanSeek);

        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);
        Assert.Equal(4, stream.Position);

        stream.StartRecording();
        stream.Read(buffer, 0, 4);
        Assert.Equal(8, stream.Position);

        stream.Rewind();
        Assert.Equal(4, stream.Position);
    }

    [Fact]
    public void TestNonSeekableStream_PositionSet_WithinBuffer()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;

        var nonSeekableStream = new NonSeekableStreamWrapper(ms);
        var stream = new RewindableStream(nonSeekableStream);
        stream.StartRecording();

        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);
        Assert.Equal(1, BitConverter.ToInt32(buffer, 0));

        stream.Read(buffer, 0, 4);
        Assert.Equal(2, BitConverter.ToInt32(buffer, 0));

        stream.Position = 0;
        Assert.Equal(0, stream.Position);

        stream.Read(buffer, 0, 4);
        Assert.Equal(1, BitConverter.ToInt32(buffer, 0));

        stream.Read(buffer, 0, 4);
        Assert.Equal(2, BitConverter.ToInt32(buffer, 0));
    }

    [Fact]
    public void TestNonSeekableStream_PositionSet_OutsideBuffer_Throws()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Flush();
        ms.Position = 0;

        var nonSeekableStream = new NonSeekableStreamWrapper(ms);
        var stream = new RewindableStream(nonSeekableStream);
        stream.StartRecording();

        var buffer = new byte[4];
        stream.Read(buffer, 0, 4);

        Assert.Throws<NotSupportedException>(() => stream.Position = 100);
    }

    [Fact]
    public void TestNonSeekableStream_StopRecordingBasic()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Flush();
        ms.Position = 0;

        var nonSeekableStream = new NonSeekableStreamWrapper(ms);
        var stream = new RewindableStream(nonSeekableStream);
        stream.StartRecording();
        var br = new BinaryReader(stream);

        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        stream.StopRecording();

        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());

        Assert.False(stream.IsRecording);
    }

    [Fact]
    public void TestStopRecordingThenRewind()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Write(8);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();
        var br = new BinaryReader(new ForwardOnlyStream(stream));

        // Read first 4 values (gets buffered)
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        // Stop recording
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // Rewind to start of buffer
        stream.Rewind(true);

        // Should be able to read from buffer again
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        // Rewind to start of buffer
        stream.Rewind();
        // Should be able to read from buffer again
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        // Continue reading remaining data from underlying stream
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
        Assert.Equal(8, br.ReadInt32());
    }

    [Fact]
    public void TestNonSeekableStream_StopRecordingThenRewind()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Write(8);
        bw.Flush();
        ms.Position = 0;

        var nonSeekableStream = new NonSeekableStreamWrapper(ms);
        var stream = new RewindableStream(nonSeekableStream);
        stream.StartRecording();
        var br = new BinaryReader(stream);

        // Read first 4 values (gets buffered)
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        // Stop recording
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // Rewind to start of buffer
        stream.Rewind(true);

        // Should be able to read from buffer again
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        // Continue reading remaining data from underlying stream
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());
        Assert.Equal(7, br.ReadInt32());
        Assert.Equal(8, br.ReadInt32());
    }

    [Fact]
    public void TestMultipleRewindsAfterStopRecording()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Write(3);
        bw.Write(4);
        bw.Write(5);
        bw.Write(6);
        bw.Write(7);
        bw.Write(8);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();
        var br = new BinaryReader(stream);

        // Read first 4 values (gets buffered)
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        // Stop recording
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // First rewind - read all buffered data, then continue with underlying stream
        stream.Rewind();
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());
        Assert.Equal(5, br.ReadInt32());
        Assert.Equal(6, br.ReadInt32());

        // Second rewind - should still be able to read from buffer
        stream.Rewind();
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        // Third rewind - still works
        stream.Rewind();
        Assert.Equal(1, br.ReadInt32());
        Assert.Equal(2, br.ReadInt32());
        Assert.Equal(3, br.ReadInt32());
        Assert.Equal(4, br.ReadInt32());

        // Continue reading from underlying stream (values 7, 8 since 5, 6 were already consumed)
        Assert.Equal(7, br.ReadInt32());
        Assert.Equal(8, br.ReadInt32());
    }

    [Fact]
    public void TestStopRecordingTwiceThrows()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());

        // First StopRecording should succeed
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // Second StopRecording should throw
        Assert.Throws<InvalidOperationException>(() => stream.StopRecording());
    }

    [Fact]
    public void TestStartRecordingAfterStopRecordingThrows()
    {
        var ms = new MemoryStream();
        var bw = new BinaryWriter(ms);
        bw.Write(1);
        bw.Write(2);
        bw.Flush();
        ms.Position = 0;

        var stream = new RewindableStream(ms);
        stream.StartRecording();

        var br = new BinaryReader(stream);
        Assert.Equal(1, br.ReadInt32());

        // Stop recording
        stream.StopRecording();
        Assert.False(stream.IsRecording);

        // Trying to start recording again should throw
        Assert.Throws<InvalidOperationException>(() => stream.StartRecording());
    }

    private class NonSeekableStreamWrapper : Stream
    {
        private readonly Stream _baseStream;

        public NonSeekableStreamWrapper(Stream baseStream)
        {
            _baseStream = baseStream;
        }

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _baseStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => _baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _baseStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
