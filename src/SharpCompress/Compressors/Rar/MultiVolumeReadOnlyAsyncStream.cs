using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Rar;

internal sealed partial class MultiVolumeReadOnlyAsyncStream
    : MultiVolumeReadOnlyStreamBase,
        IStreamStack
{
    Stream IStreamStack.BaseStream() => currentStream.NotNull();

    private long currentPosition;
    private long maxPosition;

    private IAsyncEnumerator<RarFilePart> filePartEnumerator;
    private Stream? currentStream;

    private MultiVolumeReadOnlyAsyncStream(IAsyncEnumerable<RarFilePart> parts)
    {
        filePartEnumerator = parts.GetAsyncEnumerator();
    }

    // Async methods moved to MultiVolumeReadOnlyAsyncStream.Async.cs

    private void InitializeNextFilePart()
    {
        maxPosition = filePartEnumerator.Current.FileHeader.CompressedSize;
        currentPosition = 0;
        currentStream = filePartEnumerator.Current.GetCompressedStream();

        CurrentCrc = filePartEnumerator.Current.FileHeader.FileCrc;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException(
            "Synchronous read is not supported in MultiVolumeReadOnlyAsyncStream."
        );

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
