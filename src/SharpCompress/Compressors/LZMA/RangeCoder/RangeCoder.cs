#nullable disable

using System;
using System.IO;
using System.Runtime.CompilerServices;
#if !LEGACY_DOTNET
using System.Buffers;
#endif

namespace SharpCompress.Compressors.LZMA.RangeCoder;

internal partial class Encoder
{
    public const uint K_TOP_VALUE = (1 << 24);

    private Stream _stream;

    public ulong _low;
    public uint _range;
    private uint _cacheSize;
    private byte _cache;
    private byte[] _singleByteBuffer = new byte[1];

    public void SetStream(Stream stream) => _stream = stream;

    public void ReleaseStream() => _stream = null;

    public void Init()
    {
        //StartPosition = Stream.Position;

        _low = 0;
        _range = 0xFFFFFFFF;
        _cacheSize = 1;
        _cache = 0;
    }

    public void FlushData()
    {
        for (var i = 0; i < 5; i++)
        {
            ShiftLow();
        }
    }

    public void FlushStream() => _stream.Flush();

    public void CloseStream() => _stream.Dispose();

    public void ShiftLow()
    {
        if ((uint)_low < 0xFF000000 || (uint)(_low >> 32) == 1)
        {
            var temp = _cache;
            do
            {
                _stream.WriteByte((byte)(temp + (_low >> 32)));
                temp = 0xFF;
            } while (--_cacheSize != 0);
            _cache = (byte)(((uint)_low) >> 24);
        }
        _cacheSize++;
        _low = ((uint)_low) << 8;
    }

    public void EncodeDirectBits(uint v, int numTotalBits)
    {
        for (var i = numTotalBits - 1; i >= 0; i--)
        {
            _range >>= 1;
            if (((v >> i) & 1) == 1)
            {
                _low += _range;
            }
            if (_range < K_TOP_VALUE)
            {
                _range <<= 8;
                ShiftLow();
            }
        }
    }

    public long GetProcessedSizeAdd() => -1;
}

internal partial class Decoder
{
    public const uint K_TOP_VALUE = (1 << 24);
    public uint _range;
    public uint _code;

    public Stream _stream;
    public long _total;
    private byte[] _singleByteBuffer;

#if !LEGACY_DOTNET
    // Upper bound (in terms of _total) that the fast buffered reader is allowed to physically
    // read up to. -1 means unbounded. This matters for formats like LZMA2 where multiple
    // independent chunks share one underlying stream: chunk headers are read directly from that
    // stream between decode sessions, so the fast path must not read past the end of the
    // current chunk's compressed data, or it would desynchronize the stream position for the
    // next chunk header read. Set via SetFastLimit once the caller knows the chunk/stream size.
    private long _fastLimit = -1;

    // Whether it is safe to bulk-read ahead of the decoder even without a known _fastLimit.
    // This is only true for streams that are guaranteed to self-clamp Read() to their own
    // logical end and never return bytes that belong to something else - e.g. 7Zip's
    // per-folder BufferedSubStream, which limits every Read() to its own remaining pack size.
    // It is false for everything else, notably a shared streaming Zip reader stream with an
    // unknown compressed size (data-descriptor entries): that stream keeps handing out bytes
    // past the logical end of this LZMA stream (the next entry's header, etc.) with no self
    // clamping and no way to give unread bytes back, so bulk-buffering there would
    // desynchronize the stream. Note a stream reporting a queryable Length is NOT a reliable
    // signal here: some wrapper streams (e.g. SharpCompressStream's ring-buffer mode used for
    // over-read recording on non-seekable Zip streams) expose a Length without actually
    // bounding Read() to the current logical stream's end. In the unsafe case we fall back to
    // reading exactly one byte at a time, matching the legacy per-byte ReadByte() behavior.
    private bool _fastBufferSafeUnbounded;

    public void SetFastLimit(long limit) => _fastLimit = limit;
#endif

    public void Init(Stream stream)
    {
        _stream = stream;

        _code = 0;
        _range = 0xFFFFFFFF;
        for (var i = 0; i < 5; i++)
        {
            _code = (_code << 8) | (byte)_stream.ReadByte();
        }
        _total = 5;
#if !LEGACY_DOTNET
        _fastLimit = -1;
        _fastBufferPos = 0;
        _fastBufferLen = 0;
        _fastEndOfStream = false;
        _fastBufferSafeUnbounded = stream is SharpCompress.IO.BufferedSubStream;
#endif
    }

    public void ReleaseStream()
    {
#if !LEGACY_DOTNET
        ReleaseFastBuffer();
#endif
        // Stream.ReleaseStream();
        _stream = null;
    }

#if !LEGACY_DOTNET
    // Buffered input used only by the unsafe fast LZMA decode path (see LzmaDecoder.Fast.cs).
    // Avoids issuing a virtual Stream.ReadByte() call per consumed byte, which otherwise
    // dominates decode time (millions of calls for a typical archive).
    private const int FastBufferSize = 1 << 16;
    private byte[] _fastBuffer;
    private int _fastBufferPos;
    private int _fastBufferLen;
    private bool _fastEndOfStream;

    // The fast decode loop (LzmaDecoder.Fast.cs) pins this array and keeps its own local
    // copies of position/length/consumed-byte-count for the duration of a decode call, only
    // syncing back through these accessors when the local buffer is exhausted (rare) or when
    // the decode call ends. This avoids reloading these fields from this object on every bit.
    internal byte[] FastBufferArray => _fastBuffer ??= ArrayPool<byte>.Shared.Rent(FastBufferSize);

    internal int FastBufferPos
    {
        get => _fastBufferPos;
        set => _fastBufferPos = value;
    }

    internal int FastBufferLen => _fastBufferLen;

    internal void AddTotal(long consumed) => _total += consumed;

    internal void RefillFast() => FillFastBuffer();

    private void FillFastBuffer()
    {
        _fastBuffer ??= ArrayPool<byte>.Shared.Rent(FastBufferSize);
        if (_fastEndOfStream)
        {
            // Match legacy (byte)_stream.ReadByte() behavior: once the stream is
            // exhausted, keep yielding 0xFF instead of throwing or re-reading.
            _fastBufferPos = 0;
            _fastBufferLen = 1;
            _fastBuffer[0] = 0xFF;
            return;
        }
        var requestSize = _fastBuffer.Length;
        if (_fastLimit >= 0)
        {
            var remaining = _fastLimit - _total;
            requestSize = remaining <= 0 ? 1 : (int)Math.Min(requestSize, remaining);
        }
        else if (!_fastBufferSafeUnbounded)
        {
            // No known limit and the underlying stream cannot report a bounded Length (e.g. a
            // shared, forward-only stream such as a streaming Zip reader). Reading ahead here
            // could consume bytes that belong to whatever comes after this logical LZMA stream
            // (the next Zip entry's header, etc.) with no way to give them back. Fall back to
            // requesting exactly one byte at a time so we never consume more than the decoder
            // actually needs, matching the legacy per-byte ReadByte() behavior.
            requestSize = 1;
        }
        var read = _stream.Read(_fastBuffer, 0, requestSize);
        if (read <= 0)
        {
            _fastEndOfStream = true;
            _fastBufferPos = 0;
            _fastBufferLen = 1;
            _fastBuffer[0] = 0xFF;
            return;
        }
        _fastBufferPos = 0;
        _fastBufferLen = read;
    }

    private void ReleaseFastBuffer()
    {
        if (_fastBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_fastBuffer);
            _fastBuffer = null;
        }
        _fastBufferPos = 0;
        _fastBufferLen = 0;
        _fastEndOfStream = false;
    }
#endif

    public void Normalize()
    {
        while (_range < K_TOP_VALUE)
        {
            _code = (_code << 8) | (byte)_stream.ReadByte();
            _range <<= 8;
            _total++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize2()
    {
        if (_range < K_TOP_VALUE)
        {
            _code = (_code << 8) | (byte)_stream.ReadByte();
            _range <<= 8;
            _total++;
        }
    }

    public uint GetThreshold(uint total) => _code / (_range /= total);

    public void Decode(uint start, uint size)
    {
        _code -= start * _range;
        _range *= size;
        Normalize();
    }

    public uint DecodeDirectBits(int numTotalBits)
    {
        var range = _range;
        var code = _code;
        uint result = 0;
        for (var i = numTotalBits; i > 0; i--)
        {
            range >>= 1;
            var t = (code - range) >> 31;
            code -= range & (t - 1);
            result = (result << 1) | (1 - t);

            if (range < K_TOP_VALUE)
            {
                code = (code << 8) | (byte)_stream.ReadByte();
                range <<= 8;
                _total++;
            }
        }
        _range = range;
        _code = code;
        return result;
    }

    public uint DecodeBit(uint size0, int numTotalBits)
    {
        var newBound = (_range >> numTotalBits) * size0;
        uint symbol;
        if (_code < newBound)
        {
            symbol = 0;
            _range = newBound;
        }
        else
        {
            symbol = 1;
            _code -= newBound;
            _range -= newBound;
        }
        Normalize();
        return symbol;
    }

    public bool IsFinished => _code == 0;
}
