//
// ADC.cs
//
// Author:
//       Natalia Portillo <claunia@claunia.com>
//
// Copyright (c) 2016 © Claunia.com
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.ADC;

public sealed partial class ADCStream
{
    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        if (count == 0)
        {
            return 0;
        }
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        if (offset < buffer.GetLowerBound(0))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        if ((offset + count) > buffer.GetLength(0))
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (_outBuffer is null)
        {
            var result = await ADCBase.DecompressAsync(
                _stream,
                cancellationToken: cancellationToken
            );
            _outBuffer = result.Output;
            _outPosition = 0;
        }

        var inPosition = offset;
        var toCopy = count;
        var copied = 0;

        var outBuf = _outBuffer.NotNull();
        while (_outPosition + toCopy >= outBuf.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var piece = outBuf.Length - _outPosition;
            Array.Copy(outBuf, _outPosition, buffer, inPosition, piece);
            inPosition += piece;
            copied += piece;
            _position += piece;
            toCopy -= piece;
            var result = await ADCBase.DecompressAsync(
                _stream,
                cancellationToken: cancellationToken
            );
            _outBuffer = result.Output;
            _outPosition = 0;
            if (result.BytesRead == 0 || _outBuffer is null || _outBuffer.Length == 0)
            {
                return copied;
            }
            outBuf = _outBuffer;
        }

        Array.Copy(outBuf, _outPosition, buffer, inPosition, toCopy);
        _outPosition += toCopy;
        _position += toCopy;
        copied += toCopy;
        return copied;
    }
}
