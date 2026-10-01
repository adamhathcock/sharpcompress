using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

public partial class LzmaStream : Stream, IStreamStack, IAsyncDisposable
{
    private readonly Stream? _inputStream;
    private readonly long _inputSize;
    private readonly long _outputSize;
    private readonly bool _leaveOpen;

    private readonly int _dictionarySize;
    private readonly OutWindow _outWindow = new();
    private readonly RangeCoder.Decoder _rangeDecoder = new();
    private Decoder? _decoder;

    private long _position;
    private bool _endReached;
    private long _availableBytes;
    private long _rangeDecoderLimit;
    private long _inputPosition;

    // LZMA2
    private readonly bool _isLzma2;
    private bool _uncompressedChunk;
    private bool _needDictReset = true;
    private bool _needProps = true;

    private readonly Encoder? _encoder;
    private byte[]? _asyncHeaderBuffer;
    private bool _isDisposed;
    private ReadMode _readMode;

    private enum ReadMode
    {
        None,
        Synchronous,
        Asynchronous,
    }

    private LzmaStream(
        byte[] properties,
        Stream inputStream,
        long inputSize,
        long outputSize,
        bool isLzma2,
        bool leaveOpen = false
    )
    {
        _inputStream = inputStream;
        _inputSize = inputSize;
        _outputSize = outputSize;
        _isLzma2 = isLzma2;
        _leaveOpen = leaveOpen;
        if (!isLzma2)
        {
            _dictionarySize = BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(1));
            _outWindow.Create(_dictionarySize);

            _decoder = new Decoder();
            _decoder.SetDecoderProperties(properties);
            Properties = properties;

            _availableBytes = outputSize < 0 ? long.MaxValue : outputSize;
            _rangeDecoderLimit = inputSize;
        }
        else
        {
            _dictionarySize = 2 | (properties[0] & 1);
            _dictionarySize <<= (properties[0] >> 1) + 11;

            _outWindow.Create(_dictionarySize);

            Properties = new byte[1];
            _availableBytes = 0;
        }
    }

    public static LzmaStream Create(
        byte[] properties,
        Stream inputStream,
        bool leaveOpen = false
    ) => Create(properties, inputStream, -1, -1, null, properties.Length < 5, leaveOpen);

    public static LzmaStream Create(
        byte[] properties,
        Stream inputStream,
        long inputSize,
        bool leaveOpen = false
    ) => Create(properties, inputStream, inputSize, -1, null, properties.Length < 5, leaveOpen);

    public static LzmaStream Create(
        byte[] properties,
        Stream inputStream,
        long inputSize,
        long outputSize,
        bool leaveOpen = false
    ) =>
        Create(
            properties,
            inputStream,
            inputSize,
            outputSize,
            null,
            properties.Length < 5,
            leaveOpen
        );

    public static LzmaStream Create(
        byte[] properties,
        Stream inputStream,
        long inputSize,
        long outputSize,
        Stream? presetDictionary,
        bool isLzma2,
        bool leaveOpen = false
    )
    {
        var lzma = new LzmaStream(
            properties,
            inputStream,
            inputSize,
            outputSize,
            isLzma2,
            leaveOpen
        );
        if (!isLzma2)
        {
            if (presetDictionary != null)
            {
                lzma._outWindow.Train(presetDictionary);
            }

            lzma._rangeDecoder.Init(inputStream);
#if !LEGACY_DOTNET
            // Bound the fast buffered reader to the known compressed size (when available) so
            // it never reads past this entry's data even on unbounded/shared streams. When the
            // size is unknown (e.g. Zip data-descriptor entries), RangeCoder.Decoder falls back
            // to conservative per-byte reads on streams that cannot report a safe Length.
            lzma._rangeDecoder.SetFastLimit(lzma._rangeDecoderLimit);
#endif
        }
        else
        {
            if (presetDictionary != null)
            {
                lzma._outWindow.Train(presetDictionary);
                lzma._needDictReset = false;
            }
        }
        return lzma;
    }

    private LzmaStream(LzmaEncoderProperties properties, bool isLzma2)
    {
        _isLzma2 = isLzma2;
        _availableBytes = 0;
        _endReached = true;

        if (isLzma2)
        {
            throw new NotImplementedException();
        }

        _encoder = new Encoder();
        _encoder.SetCoderProperties(properties.PropIDs, properties.Properties);
        var prop = new byte[5];
        _encoder.WriteCoderProperties(prop);
        Properties = prop;
    }

    public static LzmaStream Create(
        LzmaEncoderProperties properties,
        bool isLzma2,
        Stream outputStream
    ) => Create(properties, isLzma2, null, outputStream);

    public static LzmaStream Create(
        LzmaEncoderProperties properties,
        bool isLzma2,
        Stream? presetDictionary,
        Stream outputStream
    )
    {
        var lzma = new LzmaStream(properties, isLzma2);

        lzma._encoder!.SetStreams(null, outputStream, -1, -1);

        if (presetDictionary != null)
        {
            lzma._encoder.Train(presetDictionary);
        }
        return lzma;
    }

    public override bool CanRead => _encoder == null;

    public override bool CanSeek => false;

    public override bool CanWrite => _encoder != null;

    public override void Flush() { }

    Stream IStreamStack.BaseStream() => _inputStream!;

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        if (disposing)
        {
            if (_encoder != null)
            {
                _position = _encoder.Code(null, true);
                _encoder.Dispose();
            }
            if (!_leaveOpen)
            {
                _inputStream?.Dispose();
            }
            _outWindow.Dispose();
            ReturnAsyncHeaderBuffer();
        }
        base.Dispose(disposing);
    }

    private byte[] GetAsyncHeaderBuffer() => _asyncHeaderBuffer ??= ArrayPool<byte>.Shared.Rent(6);

    private void ReturnAsyncHeaderBuffer()
    {
        if (_asyncHeaderBuffer is null)
        {
            return;
        }

        ArrayPool<byte>.Shared.Return(_asyncHeaderBuffer);
        _asyncHeaderBuffer = null;
    }

    public override long Length => _position + _availableBytes;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count > 0)
        {
            EnsureReadMode(ReadMode.Synchronous);
        }

        if (_endReached)
        {
            return 0;
        }

        var total = 0;
        while (total < count)
        {
            if (_availableBytes == 0)
            {
                if (_isLzma2)
                {
                    DecodeChunkHeader();
                }
                else
                {
                    _endReached = true;
                }
                if (_endReached)
                {
                    break;
                }
            }

            var toProcess = count - total;
            if (toProcess > _availableBytes)
            {
                toProcess = (int)_availableBytes;
            }

            _outWindow.SetLimit(toProcess);
            if (_uncompressedChunk)
            {
                _inputPosition += _outWindow.CopyStream(_inputStream, toProcess);
            }
            else if (_decoder!.Code(_dictionarySize, _outWindow, _rangeDecoder))
            {
                HandleEndMarker();
            }

            var read = _outWindow.Read(buffer, offset, toProcess);
            total += read;
            offset += read;
            _position += read;
            _availableBytes -= read;

            if (_availableBytes == 0 && !_uncompressedChunk)
            {
                if (_isLzma2 && _decoder!.HasEndMarker)
                {
                    throw new DataErrorException();
                }

                // Check range corruption scenario
                if (
                    !_rangeDecoder.IsFinished
                    || (_rangeDecoderLimit >= 0 && _rangeDecoder._total != _rangeDecoderLimit)
                )
                {
                    // Stream might have End Of Stream marker
                    _outWindow.SetLimit(toProcess + 1);
                    if (!_decoder!.Code(_dictionarySize, _outWindow, _rangeDecoder))
                    {
                        _rangeDecoder.ReleaseStream();
                        throw new DataErrorException();
                    }
                }

                _rangeDecoder.ReleaseStream();

                _inputPosition += _rangeDecoder._total;
                if (_outWindow.HasPending)
                {
                    throw new DataErrorException();
                }
            }
        }

        if (_endReached)
        {
            if (_inputSize >= 0 && _inputPosition != _inputSize)
            {
                throw new DataErrorException();
            }
            if (_outputSize >= 0 && _position != _outputSize)
            {
                throw new DataErrorException();
            }
        }

        return total;
    }

    public override int ReadByte()
    {
        EnsureReadMode(ReadMode.Synchronous);

        if (_endReached)
        {
            return -1;
        }

        if (_availableBytes == 0)
        {
            if (_isLzma2)
            {
                DecodeChunkHeader();
            }
            else
            {
                _endReached = true;
            }
        }

        if (_endReached)
        {
            if (_inputSize >= 0 && _inputPosition != _inputSize)
            {
                throw new DataErrorException();
            }
            if (_outputSize >= 0 && _position != _outputSize)
            {
                throw new DataErrorException();
            }

            return -1;
        }

        _outWindow.SetLimit(1);
        if (_uncompressedChunk)
        {
            _inputPosition += _outWindow.CopyStream(_inputStream, 1);
        }
        else if (_decoder!.Code(_dictionarySize, _outWindow, _rangeDecoder))
        {
            HandleEndMarker();
        }

        var value = _outWindow.ReadByte();
        _position++;
        _availableBytes--;

        if (_availableBytes == 0 && !_uncompressedChunk)
        {
            if (_isLzma2 && _decoder!.HasEndMarker)
            {
                throw new DataErrorException();
            }

            // Check range corruption scenario
            if (
                !_rangeDecoder.IsFinished
                || (_rangeDecoderLimit >= 0 && _rangeDecoder._total != _rangeDecoderLimit)
            )
            {
                // Stream might have End Of Stream marker
                _outWindow.SetLimit(2);
                if (!_decoder!.Code(_dictionarySize, _outWindow, _rangeDecoder))
                {
                    _rangeDecoder.ReleaseStream();
                    throw new DataErrorException();
                }
            }

            _rangeDecoder.ReleaseStream();

            _inputPosition += _rangeDecoder._total;
            if (_outWindow.HasPending)
            {
                throw new DataErrorException();
            }
        }

        return value;
    }

    private void EnsureReadMode(ReadMode readMode)
    {
        if (_readMode == ReadMode.None)
        {
            _readMode = readMode;
            return;
        }

        if (_readMode != readMode)
        {
            throw new InvalidOperationException(
                "A LzmaStream cannot mix synchronous and asynchronous reads."
            );
        }
    }

    private void DecodeChunkHeader()
    {
        var control = _inputStream!.ReadByte();
        _inputPosition++;

        if (control == 0x00)
        {
            if (_isLzma2 && _decoder is { HasEndMarker: true })
            {
                throw new DataErrorException();
            }

            _endReached = true;
            return;
        }

        if (control >= 0xE0 || control == 0x01)
        {
            _needProps = true;
            _needDictReset = false;
            _outWindow.Reset();
        }
        else if (_needDictReset)
        {
            throw new DataErrorException();
        }

        if (control >= 0x80)
        {
            _uncompressedChunk = false;

            _availableBytes = (control & 0x1F) << 16;
            _availableBytes += (_inputStream.ReadByte() << 8) + _inputStream.ReadByte() + 1;
            _inputPosition += 2;

            _rangeDecoderLimit = (_inputStream.ReadByte() << 8) + _inputStream.ReadByte() + 1;
            _inputPosition += 2;

            if (control >= 0xC0)
            {
                _needProps = false;
                Properties[0] = (byte)_inputStream.ReadByte();
                _inputPosition++;

                _decoder = new Decoder();
                _decoder.SetDecoderProperties(Properties);
            }
            else if (_needProps)
            {
                throw new DataErrorException();
            }
            else if (control >= 0xA0)
            {
                _decoder = new Decoder();
                _decoder.SetDecoderProperties(Properties);
            }

            _rangeDecoder.Init(_inputStream);
#if !LEGACY_DOTNET
            // LZMA2 chunks share one underlying stream with the raw chunk-header bytes read
            // above/below, so the buffered fast-read path (see RangeCoder.Decoder's fast
            // buffer) must never physically read past this chunk's compressed size, or it
            // would desynchronize the stream position for the next chunk header.
            _rangeDecoder.SetFastLimit(_rangeDecoderLimit);
#endif
        }
        else if (control > 0x02)
        {
            throw new DataErrorException();
        }
        else
        {
            _uncompressedChunk = true;
            _availableBytes = (_inputStream.ReadByte() << 8) + _inputStream.ReadByte() + 1;
            _inputPosition += 2;
        }
    }

    private void HandleEndMarker()
    {
        if (_isLzma2)
        {
            throw new DataErrorException();
        }

        if (_outputSize < 0)
        {
            _availableBytes = _outWindow.AvailableBytes;
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_encoder != null)
        {
            _position = _encoder.Code(new MemoryStream(buffer, offset, count), false);
        }
    }

    public byte[] Properties { get; } = new byte[5];

    internal long CompressedBytesRead => _inputPosition;
}
