#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Rar;

internal sealed class MultiVolumeReadOnlyStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => currentStream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private long currentPosition;
    private long maxPosition;

    private IEnumerator<RarFilePart> filePartEnumerator;
    private IAsyncEnumerator<RarFilePart> filePartAsyncEnumerator;
    private Stream currentStream;

    internal MultiVolumeReadOnlyStream(IEnumerable<RarFilePart> parts)
    {
        filePartEnumerator = parts.GetEnumerator();
        filePartEnumerator.MoveNext();
        InitializeNextFilePart();
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(MultiVolumeReadOnlyStream));
#endif
    }

    internal MultiVolumeReadOnlyStream(
        IAsyncEnumerable<RarFilePart> parts,
        CancellationToken cancellationToken = default
    )
    {
        filePartAsyncEnumerator = parts.GetAsyncEnumerator(cancellationToken);
        // Initialize asynchronously - need to call MoveNextAsync and InitializeNextFilePartAsync
        // This will be done in a helper method
        filePartAsyncEnumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult();
        InitializeNextFilePart();
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(MultiVolumeReadOnlyStream));
#endif
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
#if DEBUG_STREAMS
            this.DebugDispose(typeof(MultiVolumeReadOnlyStream));
#endif

            if (filePartEnumerator != null)
            {
                filePartEnumerator.Dispose();
                filePartEnumerator = null;
            }
            if (filePartAsyncEnumerator != null)
            {
                filePartAsyncEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
                filePartAsyncEnumerator = null;
            }
            currentStream = null;
        }
    }

    private void InitializeNextFilePart()
    {
        var current = filePartEnumerator?.Current ?? filePartAsyncEnumerator?.Current.NotNull();
        maxPosition = current.FileHeader.CompressedSize;
        currentPosition = 0;
        currentStream = current.GetCompressedStream();

        CurrentCrc = current.FileHeader.FileCrc;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;
        var currentOffset = offset;
        var currentCount = count;
        while (currentCount > 0)
        {
            var readSize = currentCount;
            if (currentCount > maxPosition - currentPosition)
            {
                readSize = (int)(maxPosition - currentPosition);
            }

            var read = currentStream.Read(buffer, currentOffset, readSize);
            if (read < 0)
            {
                throw new EndOfStreamException();
            }

            currentPosition += read;
            currentOffset += read;
            currentCount -= read;
            totalRead += read;

            var current = filePartEnumerator?.Current ?? filePartAsyncEnumerator?.Current.NotNull();
            if (((maxPosition - currentPosition) == 0) && current.FileHeader.IsSplitAfter)
            {
                if (current.FileHeader.R4Salt != null)
                {
                    throw new InvalidFormatException(
                        "Sharpcompress currently does not support multi-volume decryption."
                    );
                }
                var fileName = current.FileHeader.FileName;
                bool hasNext;
                if (filePartEnumerator != null)
                {
                    hasNext = filePartEnumerator.MoveNext();
                }
                else
                {
                    hasNext = filePartAsyncEnumerator
                        .NotNull()
                        .MoveNextAsync()
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                }
                if (!hasNext)
                {
                    throw new InvalidFormatException(
                        "Multi-part rar file is incomplete.  Entry expects a new volume: "
                            + fileName
                    );
                }
                InitializeNextFilePart();
            }
            else
            {
                break;
            }
        }
        return totalRead;
    }

    public override async System.Threading.Tasks.Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        System.Threading.CancellationToken cancellationToken
    )
    {
        var totalRead = 0;
        var currentOffset = offset;
        var currentCount = count;
        while (currentCount > 0)
        {
            var readSize = currentCount;
            if (currentCount > maxPosition - currentPosition)
            {
                readSize = (int)(maxPosition - currentPosition);
            }

            var read = await currentStream
                .ReadAsync(buffer, currentOffset, readSize, cancellationToken)
                .ConfigureAwait(false);
            if (read < 0)
            {
                throw new EndOfStreamException();
            }

            currentPosition += read;
            currentOffset += read;
            currentCount -= read;
            totalRead += read;

            var current = filePartEnumerator?.Current ?? filePartAsyncEnumerator?.Current.NotNull();
            if (((maxPosition - currentPosition) == 0) && current.FileHeader.IsSplitAfter)
            {
                if (current.FileHeader.R4Salt != null)
                {
                    throw new InvalidFormatException(
                        "Sharpcompress currently does not support multi-volume decryption."
                    );
                }
                var fileName = current.FileHeader.FileName;
                bool hasNext;
                if (filePartAsyncEnumerator != null)
                {
                    hasNext = await filePartAsyncEnumerator.MoveNextAsync();
                }
                else
                {
                    hasNext = filePartEnumerator.NotNull().MoveNext();
                }
                if (!hasNext)
                {
                    throw new InvalidFormatException(
                        "Multi-part rar file is incomplete.  Entry expects a new volume: "
                            + fileName
                    );
                }
                InitializeNextFilePart();
            }
            else
            {
                break;
            }
        }
        return totalRead;
    }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    public override async System.Threading.Tasks.ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        System.Threading.CancellationToken cancellationToken = default
    )
    {
        var totalRead = 0;
        var currentOffset = 0;
        var currentCount = buffer.Length;
        while (currentCount > 0)
        {
            var readSize = currentCount;
            if (currentCount > maxPosition - currentPosition)
            {
                readSize = (int)(maxPosition - currentPosition);
            }

            var read = await currentStream
                .ReadAsync(buffer.Slice(currentOffset, readSize), cancellationToken)
                .ConfigureAwait(false);
            if (read < 0)
            {
                throw new EndOfStreamException();
            }

            currentPosition += read;
            currentOffset += read;
            currentCount -= read;
            totalRead += read;

            var current = filePartEnumerator?.Current ?? filePartAsyncEnumerator?.Current.NotNull();
            if (((maxPosition - currentPosition) == 0) && current.FileHeader.IsSplitAfter)
            {
                if (current.FileHeader.R4Salt != null)
                {
                    throw new InvalidFormatException(
                        "Sharpcompress currently does not support multi-volume decryption."
                    );
                }
                var fileName = current.FileHeader.FileName;
                bool hasNext;
                if (filePartAsyncEnumerator != null)
                {
                    hasNext = await filePartAsyncEnumerator.MoveNextAsync();
                }
                else
                {
                    hasNext = filePartEnumerator.NotNull().MoveNext();
                }
                if (!hasNext)
                {
                    throw new InvalidFormatException(
                        "Multi-part rar file is incomplete.  Entry expects a new volume: "
                            + fileName
                    );
                }
                InitializeNextFilePart();
            }
            else
            {
                break;
            }
        }
        return totalRead;
    }
#endif

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public byte[] CurrentCrc { get; private set; }

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
