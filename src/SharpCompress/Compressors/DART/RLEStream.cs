//
// RLEStream.cs
//
// Author:
//       Natalia Portillo <claunia@claunia.com>
//
// Copyright (c) 2016 Natalia Portillo
// Based on libdc42.c (c) 1998-2010 Ray A. Arachelian
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

namespace SharpCompress.Compressors.DART
{
    /// <summary>
    /// This class handles the RLE algorithm used by Apple's DART.
    /// Compression should be easy, but it's not implemented.
    /// Basically it's simply a big-endian 16-bit run-length encoding.
    /// </summary>
    public class RLEStream : Stream
    {
        /// <summary>
        /// This stream holds the compressed data
        /// </summary>
        private readonly Stream stream;

        /// <summary>
        /// Is this instance disposed?
        /// </summary>
        private bool isDisposed;

        /// <summary>
        /// Position in decompressed data
        /// </summary>
        private long position;

        /// <summary>
        /// Buffer with currently used chunk of decompressed data
        /// </summary>
        private byte[] cmpBuffer;

        /// <summary>
        /// Position in buffer of decompressed data
        /// </summary>
        private int bufferPosition;

        /// <summary>
        /// Initializates a stream that decompresses ADC data on the fly
        /// </summary>
        /// <param name="stream">Stream that contains the compressed data</param>
        /// <param name="compressionMode">Must be set to <see cref="CompressionMode.Decompress"/> because compression is not implemented</param>
        public RLEStream(Stream stream, CompressionMode compressionMode = CompressionMode.Decompress)
        {
            if (compressionMode == CompressionMode.Compress)
            {
                throw new NotSupportedException();
            }

            this.stream = stream;
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                return position;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public override void Flush()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return 0;
            }
            if (buffer == null)
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

            int copied = 0;

        fillBuffer:
            if (cmpBuffer == null || bufferPosition == cmpBuffer.Length)
            {
                if (stream.Position + 4 >= stream.Length)
                    return copied;

                byte c1 = (byte)stream.ReadByte();
                byte c2 = (byte)stream.ReadByte();
                short size = (short)((c1 << 8) | c2);

                if (size == 0)
                {
                    goto fillBuffer;
                }
                else if (size < 0)
                {
                    size *= -1;
                    cmpBuffer = new byte[size * 2];
                    byte w1 = (byte)stream.ReadByte();
                    byte w2 = (byte)stream.ReadByte();
                    for (int i = 0; i < cmpBuffer.Length; i += 2)
                    {
                        cmpBuffer[i] = w1;
                        cmpBuffer[i + 1] = w2;
                    }
                }
                else
                {
                    cmpBuffer = new byte[size * 2];
                    stream.Read(cmpBuffer, 0, cmpBuffer.Length);
                }

                bufferPosition = 0;
            }

            if (bufferPosition + count <= cmpBuffer.Length)
            {
                Array.Copy(cmpBuffer, bufferPosition, buffer, offset, count);
                bufferPosition += count;
                copied += count;
            }
            else
            {
                int rest = cmpBuffer.Length - bufferPosition;
                Array.Copy(cmpBuffer, bufferPosition, buffer, offset, rest);
                count -= rest;
                offset += rest;
                copied += rest;
                cmpBuffer = null;
                goto fillBuffer;
            }

            position += copied;

            return copied;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
