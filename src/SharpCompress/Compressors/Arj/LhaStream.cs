using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Arj;

[CLSCompliant(true)]
public sealed partial class LhaStream<TDecoderConfig> : Stream, IFaultableStream
    where TDecoderConfig : ILhaDecoderConfig, new()
{
    private readonly BitReader _bitReader;

    private readonly HuffTree _commandTree;
    private readonly HuffTree _offsetTree;
    private int _remainingCommands;

    // A back-reference can produce more bytes than fit in the caller's buffer, so the
    // unfinished part is kept here and resumed on the next read.
    private int _pendingCopyOffset;
    private int _pendingCopyCount;
    private readonly RingBuffer _ringBuffer;
    private readonly TDecoderConfig _config = new TDecoderConfig();

    private const int NUM_COMMANDS = 510;
    private const int NUM_TEMP_CODELEN = 20;

    private readonly int _originalSize;
    private int _producedBytes = 0;

    // Set when a read is canceled after it has started consuming decoder state; the
    // decoder can no longer be trusted to be in sync with the bit stream.
    private bool _faulted;

    bool IFaultableStream.IsFaulted => _faulted;

    public LhaStream(Stream compressedStream, int originalSize)
    {
        _bitReader = new BitReader(compressedStream);
        _ringBuffer = _config.RingBuffer;
        _commandTree = new HuffTree(NUM_COMMANDS * 2);
        _offsetTree = new HuffTree(NUM_TEMP_CODELEN * 2);
        _remainingCommands = 0;
        _originalSize = originalSize;
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

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private int CopyFromHistory(byte[] target, int targetIndex, int maxCount)
    {
        // The iterator pushes every copied byte back into the ring buffer, which advances the
        // cursor; resuming from the same offset later therefore continues where this stopped.
        HistoryIterator historyIter = _ringBuffer.IterFromOffset(_pendingCopyOffset);
        int count = Math.Min(_pendingCopyCount, maxCount);

        for (int i = 0; i < count; i++)
        {
            historyIter.MoveNext();
            target[targetIndex + i] = historyIter.Current;
        }

        _pendingCopyCount -= count;
        return count;
    }
}
