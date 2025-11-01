using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

internal class Bcj2DecoderStream : DecoderStream2, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _mMainStream;

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

    private const int K_NUM_TOP_BITS = 24;
    private const uint K_TOP_VALUE = (1 << K_NUM_TOP_BITS);

    private class RangeDecoder
    {
        internal readonly Stream _mStream;
        internal uint _range;
        internal uint _code;

        public RangeDecoder(Stream stream)
        {
            _mStream = stream;
            _range = 0xFFFFFFFF;
            for (var i = 0; i < 5; i++)
            {
                _code = (_code << 8) | ReadByte();
            }
        }

        public byte ReadByte()
        {
            var bt = _mStream.ReadByte();
            if (bt < 0)
            {
                throw new EndOfStreamException();
            }

            return (byte)bt;
        }

        public void Dispose() => _mStream.Dispose();
    }

    private class StatusDecoder
    {
        private const int NUM_MOVE_BITS = 5;

        private const int K_NUM_BIT_MODEL_TOTAL_BITS = 11;
        private const uint K_BIT_MODEL_TOTAL = 1u << K_NUM_BIT_MODEL_TOTAL_BITS;

        private uint _prob;

        public StatusDecoder() => _prob = K_BIT_MODEL_TOTAL / 2;

        public uint Decode(RangeDecoder decoder)
        {
            var newBound = (decoder._range >> K_NUM_BIT_MODEL_TOTAL_BITS) * _prob;
            if (decoder._code < newBound)
            {
                decoder._range = newBound;
                _prob += (K_BIT_MODEL_TOTAL - _prob) >> NUM_MOVE_BITS;
                if (decoder._range < K_TOP_VALUE)
                {
                    decoder._code = (decoder._code << 8) | decoder.ReadByte();
                    decoder._range <<= 8;
                }
                return 0;
            }
            decoder._range -= newBound;
            decoder._code -= newBound;
            _prob -= _prob >> NUM_MOVE_BITS;
            if (decoder._range < K_TOP_VALUE)
            {
                decoder._code = (decoder._code << 8) | decoder.ReadByte();
                decoder._range <<= 8;
            }
            return 1;
        }
    }

    private readonly Stream _mMainStream;
    private readonly Stream _mCallStream;
    private readonly Stream _mJumpStream;
    private readonly RangeDecoder _mRangeDecoder;
    private readonly StatusDecoder[] _mStatusDecoder;
    private long _mWritten;
    private readonly IEnumerator<byte> _mIter;
    private bool _mFinished;
    private bool _isDisposed;

    public Bcj2DecoderStream(Stream[] streams, byte[] info, long limit)
    {
        if (info != null && info.Length > 0)
        {
            throw new NotSupportedException();
        }

        if (streams.Length != 4)
        {
            throw new NotSupportedException();
        }

        _mMainStream = streams[0];
        _mCallStream = streams[1];
        _mJumpStream = streams[2];
        _mRangeDecoder = new RangeDecoder(streams[3]);

        _mStatusDecoder = new StatusDecoder[256 + 2];
        for (var i = 0; i < _mStatusDecoder.Length; i++)
        {
            _mStatusDecoder[i] = new StatusDecoder();
        }

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(Bcj2DecoderStream));
#endif

        _mIter = Run().GetEnumerator();
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(Bcj2DecoderStream));
#endif
        base.Dispose(disposing);
        _mMainStream.Dispose();
        _mCallStream.Dispose();
        _mJumpStream.Dispose();
    }

    private static bool IsJcc(byte b0, byte b1) => b0 == 0x0F && (b1 & 0xF0) == 0x80;

    private static bool IsJ(byte b0, byte b1) => (b1 & 0xFE) == 0xE8 || IsJcc(b0, b1);

    private static int GetIndex(byte b0, byte b1)
    {
        if (b1 == 0xE8)
        {
            return b0;
        }
        if (b1 == 0xE9)
        {
            return 256;
        }
        return 257;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count == 0 || _mFinished)
        {
            return 0;
        }

        for (var i = 0; i < count; i++)
        {
            if (!_mIter.MoveNext())
            {
                _mFinished = true;
                return i;
            }

            buffer[offset + i] = _mIter.Current;
        }

        return count;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Bcj2DecoderStream uses complex state machine with multiple streams
        return Task.FromResult(Read(buffer, offset, count));
    }

    public override int ReadByte()
    {
        if (_mFinished)
        {
            return -1;
        }

        if (!_mIter.MoveNext())
        {
            _mFinished = true;
            return -1;
        }

        return _mIter.Current;
    }

    public IEnumerable<byte> Run()
    {
        const uint kBurstSize = (1u << 18);

        byte prevByte = 0;
        uint processedBytes = 0;
        for (; ; )
        {
            byte b = 0;
            uint i;
            for (i = 0; i < kBurstSize; i++)
            {
                var tmp = _mMainStream.ReadByte();
                if (tmp < 0)
                {
                    yield break;
                }

                b = (byte)tmp;
                _mWritten++;
                yield return b;
                if (IsJ(prevByte, b))
                {
                    break;
                }

                prevByte = b;
            }

            processedBytes += i;
            if (i == kBurstSize)
            {
                continue;
            }

            if (_mStatusDecoder[GetIndex(prevByte, b)].Decode(_mRangeDecoder) == 1)
            {
                var s = (b == 0xE8) ? _mCallStream : _mJumpStream;

                uint src = 0;
                for (i = 0; i < 4; i++)
                {
                    var b0 = s.ReadByte();
                    if (b0 < 0)
                    {
                        throw new EndOfStreamException();
                    }

                    src <<= 8;
                    src |= (uint)b0;
                }

                var dest = src - (uint)(_mWritten + 4);
                _mWritten++;
                yield return (byte)dest;
                _mWritten++;
                yield return (byte)(dest >> 8);
                _mWritten++;
                yield return (byte)(dest >> 16);
                _mWritten++;
                yield return (byte)(dest >> 24);
                prevByte = (byte)(dest >> 24);
                processedBytes += 4;
            }
            else
            {
                prevByte = b;
            }
        }
    }
}
