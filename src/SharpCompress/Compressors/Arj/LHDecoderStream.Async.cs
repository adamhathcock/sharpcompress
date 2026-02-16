using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Arj;

public sealed partial class LHDecoderStream
{
    /// <summary>
    /// Asynchronously decodes a single element (literal or back-reference) and appends it to _buffer.
    /// Returns true if data was added, or false if all input has already been decoded.
    /// </summary>
    private async ValueTask<bool> DecodeNextAsync(CancellationToken cancellationToken)
    {
        if (_buffer.Count >= _originalSize)
        {
            _finishedDecoding = true;
            return false;
        }

        int len = await DecodeValAsync(0, 7, cancellationToken).ConfigureAwait(false);
        if (len == 0)
        {
            byte nextChar = (byte)
                await _bitReader.ReadBitsAsync(8, cancellationToken).ConfigureAwait(false);
            _buffer.Add(nextChar);
        }
        else
        {
            int repCount = len + THRESHOLD - 1;
            int backPtr = await DecodeValAsync(9, 13, cancellationToken).ConfigureAwait(false);

            if (backPtr >= _buffer.Count)
            {
                throw new InvalidFormatException("Invalid back_ptr in LH stream");
            }

            int srcIndex = _buffer.Count - 1 - backPtr;
            for (int j = 0; j < repCount && _buffer.Count < _originalSize; j++)
            {
                byte b = _buffer[srcIndex];
                _buffer.Add(b);
                srcIndex++;
                // srcIndex may grow; it's allowed (source region can overlap destination)
            }
        }

        if (_buffer.Count >= _originalSize)
        {
            _finishedDecoding = true;
        }

        return true;
    }

    private async ValueTask<int> DecodeValAsync(
        int from,
        int to,
        CancellationToken cancellationToken
    )
    {
        int add = 0;
        int bit = from;

        while (
            bit < to
            && await _bitReader.ReadBitsAsync(1, cancellationToken).ConfigureAwait(false) == 1
        )
        {
            add |= 1 << bit;
            bit++;
        }

        int res =
            bit > 0
                ? await _bitReader.ReadBitsAsync(bit, cancellationToken).ConfigureAwait(false)
                : 0;
        return res + add;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LHDecoderStream));
        }

        ThrowHelper.ThrowIfNull(buffer);

        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset/count");
        }

        if (_readPosition >= _originalSize)
        {
            return 0; // EOF
        }

        int totalRead = 0;

        while (totalRead < count && _readPosition < _originalSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_readPosition >= _buffer.Count)
            {
                bool had = await DecodeNextAsync(cancellationToken).ConfigureAwait(false);
                if (!had)
                {
                    break;
                }
            }

            int available = _buffer.Count - (int)_readPosition;
            if (available <= 0)
            {
                if (!_finishedDecoding)
                {
                    continue;
                }
                break;
            }

            int toCopy = Math.Min(available, count - totalRead);
            _buffer.CopyTo((int)_readPosition, buffer, offset + totalRead, toCopy);

            _readPosition += toCopy;
            totalRead += toCopy;
        }

        return totalRead;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LHDecoderStream));
        }

        if (_readPosition >= _originalSize)
        {
            return 0; // EOF
        }

        int totalRead = 0;

        while (totalRead < buffer.Length && _readPosition < _originalSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_readPosition >= _buffer.Count)
            {
                bool had = await DecodeNextAsync(cancellationToken).ConfigureAwait(false);
                if (!had)
                {
                    break;
                }
            }

            int available = _buffer.Count - (int)_readPosition;
            if (available <= 0)
            {
                if (!_finishedDecoding)
                {
                    continue;
                }
                break;
            }

            int toCopy = Math.Min(available, buffer.Length - totalRead);
            for (int i = 0; i < toCopy; i++)
            {
                buffer.Span[totalRead + i] = _buffer[(int)_readPosition + i];
            }

            _readPosition += toCopy;
            totalRead += toCopy;
        }

        return totalRead;
    }
#endif
}
