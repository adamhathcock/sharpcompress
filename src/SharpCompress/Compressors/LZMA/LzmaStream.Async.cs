using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.LZMA;

public partial class LzmaStream
{
    public static ValueTask<LzmaStream> CreateAsync(
        byte[] properties,
        Stream inputStream,
        long inputSize,
        long outputSize,
        bool leaveOpen = false
    ) =>
        CreateAsync(
            properties,
            inputStream,
            inputSize,
            outputSize,
            null,
            properties.Length < 5,
            leaveOpen
        );

    public static async ValueTask<LzmaStream> CreateAsync(
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
                await lzma._outWindow.TrainAsync(presetDictionary);
            }

            await lzma._rangeDecoder.InitAsync(inputStream);
        }
        else
        {
            if (presetDictionary != null)
            {
                await lzma._outWindow.TrainAsync(presetDictionary);
                lzma._needDictReset = false;
            }
        }
        return lzma;
    }

    /*public static async ValueTask<LzmaStream> CreateAsync(
        LzmaEncoderProperties properties,
        bool isLzma2,
        Stream? presetDictionary,
        Stream outputStream
    )
    {
        var lzma = new LzmaStream(properties, isLzma2, presetDictionary);

        lzma._encoder!.SetStreams(null, outputStream, -1, -1);

        if (presetDictionary != null)
        {
            lzma._encoder.Train(presetDictionary);
        }
        return lzma;
    }*/

    private async ValueTask DecodeChunkHeaderAsync(CancellationToken cancellationToken = default)
    {
        var controlBuffer = new byte[1];
        await _inputStream!
            .ReadExactAsync(controlBuffer, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        var control = controlBuffer[0];
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
            var buffer = new byte[2];
            await _inputStream!
                .ReadExactAsync(buffer, 0, 2, cancellationToken)
                .ConfigureAwait(false);
            _availableBytes += (buffer[0] << 8) + buffer[1] + 1;
            _inputPosition += 2;

            await _inputStream!
                .ReadExactAsync(buffer, 0, 2, cancellationToken)
                .ConfigureAwait(false);
            _rangeDecoderLimit = (buffer[0] << 8) + buffer[1] + 1;
            _inputPosition += 2;

            if (control >= 0xC0)
            {
                _needProps = false;
                await _inputStream!
                    .ReadExactAsync(controlBuffer, 0, 1, cancellationToken)
                    .ConfigureAwait(false);
                Properties[0] = controlBuffer[0];
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

            await _rangeDecoder.InitAsync(_inputStream, cancellationToken);
        }
        else if (control > 0x02)
        {
            throw new DataErrorException();
        }
        else
        {
            _uncompressedChunk = true;
            var buffer = new byte[2];
            await _inputStream!
                .ReadExactAsync(buffer, 0, 2, cancellationToken)
                .ConfigureAwait(false);
            _availableBytes = (buffer[0] << 8) + buffer[1] + 1;
            _inputPosition += 2;
        }
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_endReached)
        {
            return 0;
        }

        var total = 0;
        while (total < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_availableBytes == 0)
            {
                if (_isLzma2)
                {
                    await DecodeChunkHeaderAsync(cancellationToken).ConfigureAwait(false);
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
                _inputPosition += await _outWindow
                    .CopyStreamAsync(_inputStream, toProcess, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (
                await _decoder!
                    .CodeAsync(_dictionarySize, _outWindow, _rangeDecoder, cancellationToken)
                    .ConfigureAwait(false)
                && _outputSize < 0
            )
            {
                _availableBytes = _outWindow.AvailableBytes;
            }

            var read = _outWindow.Read(buffer, offset, toProcess);
            total += read;
            offset += read;
            _position += read;
            _availableBytes -= read;

            if (_availableBytes == 0 && !_uncompressedChunk)
            {
                if (
                    !_rangeDecoder.IsFinished
                    || (_rangeDecoderLimit >= 0 && _rangeDecoder._total != _rangeDecoderLimit)
                )
                {
                    _outWindow.SetLimit(toProcess + 1);
                    if (
                        !await _decoder!
                            .CodeAsync(
                                _dictionarySize,
                                _outWindow,
                                _rangeDecoder,
                                cancellationToken
                            )
                            .ConfigureAwait(false)
                    )
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

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_endReached)
        {
            return 0;
        }

        var total = 0;
        var offset = 0;
        var count = buffer.Length;
        while (total < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_availableBytes == 0)
            {
                if (_isLzma2)
                {
                    await DecodeChunkHeaderAsync(cancellationToken).ConfigureAwait(false);
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
                _inputPosition += await _outWindow
                    .CopyStreamAsync(_inputStream, toProcess, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (
                await _decoder!
                    .CodeAsync(_dictionarySize, _outWindow, _rangeDecoder, cancellationToken)
                    .ConfigureAwait(false)
                && _outputSize < 0
            )
            {
                _availableBytes = _outWindow.AvailableBytes;
            }

            var read = _outWindow.Read(buffer, offset, toProcess);
            total += read;
            offset += read;
            _position += read;
            _availableBytes -= read;

            if (_availableBytes == 0 && !_uncompressedChunk)
            {
                if (
                    !_rangeDecoder.IsFinished
                    || (_rangeDecoderLimit >= 0 && _rangeDecoder._total != _rangeDecoderLimit)
                )
                {
                    _outWindow.SetLimit(toProcess + 1);
                    if (
                        !await _decoder!
                            .CodeAsync(
                                _dictionarySize,
                                _outWindow,
                                _rangeDecoder,
                                cancellationToken
                            )
                            .ConfigureAwait(false)
                    )
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
#endif

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer, offset, count);
        return Task.CompletedTask;
    }
}
