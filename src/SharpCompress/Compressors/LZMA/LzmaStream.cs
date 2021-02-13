#nullable disable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA.LZ;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA
{
    public class LzmaStream : AsyncStream
    {
        private Stream _inputStream;
        private long _inputSize;
        private long _outputSize;

        private int _dictionarySize;
        private OutWindow _outWindow = new OutWindow();
        private RangeCoder.Decoder _rangeDecoder = new RangeCoder.Decoder();
        private Decoder _decoder;

        private long _position;
        private bool _endReached;
        private long _availableBytes;
        private long _rangeDecoderLimit;
        private long _inputPosition;

        // LZMA2
        private bool _isLzma2;
        private bool _uncompressedChunk;
        private bool _needDictReset = true;
        private bool _needProps = true;

        private readonly Encoder _encoder;
        private bool _isDisposed;
        
        private LzmaStream() {}

        public static async ValueTask<LzmaStream> CreateAsync(byte[] properties, Stream inputStream, long inputSize = -1, long outputSize = -1,
                                                       Stream presetDictionary = null, bool? isLzma2 = null)
        {
            var ls = new LzmaStream();
            ls._inputStream = inputStream;
            ls._inputSize = inputSize;
            ls._outputSize = outputSize;
            ls._isLzma2 = isLzma2 ?? properties.Length < 5;

            if (!ls._isLzma2)
            {
                ls._dictionarySize = BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(1));
                ls._outWindow.Create(ls._dictionarySize);
                if (presetDictionary != null)
                {
                    ls._outWindow.Train(presetDictionary);
                }

                await ls._rangeDecoder.InitAsync(inputStream);

                ls._decoder = new Decoder();
                ls._decoder.SetDecoderProperties(properties);
                ls.Properties = properties;

                ls._availableBytes = outputSize < 0 ? long.MaxValue : outputSize;
                ls._rangeDecoderLimit = inputSize;
            }
            else
            {
                ls. _dictionarySize = 2 | (properties[0] & 1);
                ls. _dictionarySize <<= (properties[0] >> 1) + 11;

                ls._outWindow.Create(ls._dictionarySize);
                if (presetDictionary != null)
                {
                    ls._outWindow.Train(presetDictionary);
                    ls._needDictReset = false;
                }

                ls. Properties = new byte[1];
                ls._availableBytes = 0;
            }

            return ls;
        }

        public LzmaStream(LzmaEncoderProperties properties, bool isLzma2, Stream outputStream)
            : this(properties, isLzma2, null, outputStream)
        {
        }

        public LzmaStream(LzmaEncoderProperties properties, bool isLzma2, Stream presetDictionary, Stream outputStream)
        {
            _isLzma2 = isLzma2;
            _availableBytes = 0;
            _endReached = true;

            if (isLzma2)
            {
                throw new NotImplementedException();
            }

            _encoder = new Encoder();
            _encoder.SetCoderProperties(properties._propIDs, properties._properties);
            byte[] prop = new byte[5];
            _encoder.WriteCoderProperties(prop);
            Properties = prop;

            _encoder.SetStreams(null, outputStream, -1, -1);
            if (presetDictionary != null)
            {
                _encoder.Train(presetDictionary);
            }
        }

        public override bool CanRead => _encoder == null;

        public override bool CanSeek => false;

        public override bool CanWrite => _encoder != null;

        public override async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            if (_encoder != null)
            {
                _position = await _encoder.CodeAsync(null, true);
            }
            _inputStream?.DisposeAsync();
        }

        public override long Length => _position + _availableBytes;

        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_endReached)
            {
                return 0;
            }

            int total = 0;
            while (total < count)
            {
                if (_availableBytes == 0)
                {
                    if (_isLzma2)
                    {
                        await DecodeChunkHeader();
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

                int toProcess = count - total;
                if (toProcess > _availableBytes)
                {
                    toProcess = (int)_availableBytes;
                }

                _outWindow.SetLimit(toProcess);
                if (_uncompressedChunk)
                {
                    _inputPosition += _outWindow.CopyStream(_inputStream, toProcess);
                }
                else if (_decoder.Code(_dictionarySize, _outWindow, _rangeDecoder)
                         && _outputSize < 0)
                {
                    _availableBytes = _outWindow.AvailableBytes;
                }

                int read = _outWindow.Read(buffer, offset, toProcess);
                total += read;
                offset += read;
                _position += read;
                _availableBytes -= read;

                if (_availableBytes == 0 && !_uncompressedChunk)
                {
                    _rangeDecoder.ReleaseStream();
                    if (!_rangeDecoder.IsFinished || (_rangeDecoderLimit >= 0 && _rangeDecoder._total != _rangeDecoderLimit))
                    {
                        throw new DataErrorException();
                    }
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

        private async ValueTask DecodeChunkHeader()
        {
            int control = _inputStream.ReadByte();
            _inputPosition++;

            if (control == 0x00)
            {
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

                await _rangeDecoder.InitAsync(_inputStream);
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

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_encoder != null)
            {
                _position = await _encoder.CodeAsync(new MemoryStream(buffer, offset, count), false);
            }
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
        {
            if (_encoder != null)
            {
                 var m = ArrayPool<byte>.Shared.Rent(buffer.Length);
                 buffer.CopyTo(m.AsMemory().Slice(0, buffer.Length));
                _position = await _encoder.CodeAsync(new MemoryStream(m, 0, buffer.Length), false);
                ArrayPool<byte>.Shared.Return(m);
            }
        }

        public byte[] Properties { get; private set; }
    }
}
