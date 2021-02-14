#nullable disable

// ZlibBaseStream.cs
// ------------------------------------------------------------------
//
// Copyright (c) 2009 Dino Chiesa and Microsoft Corporation.
// All rights reserved.
//
// This code module is part of DotNetZip, a zipfile class library.
//
// ------------------------------------------------------------------
//
// This code is licensed under the Microsoft Public License.
// See the file License.txt for the license details.
// More info on: http://dotnetzip.codeplex.com
//
// ------------------------------------------------------------------
//
// last saved (in emacs):
// Time-stamp: <2009-October-28 15:45:15>
//
// ------------------------------------------------------------------
//
// This module defines the ZlibBaseStream class, which is an intnernal
// base class for DeflateStream, ZlibStream and GZipStream.
//
// ------------------------------------------------------------------

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate
{
    internal enum ZlibStreamFlavor
    {
        ZLIB = 1950,
        DEFLATE = 1951,
        GZIP = 1952
    }

    internal class ZlibBaseStream : AsyncStream
    {
        protected internal ZlibCodec _z; // deferred init... new ZlibCodec();

        protected internal StreamMode _streamMode = StreamMode.Undefined;
        protected internal FlushType _flushMode;
        private readonly ZlibStreamFlavor _flavor;
        private readonly CompressionMode _compressionMode;
        private readonly CompressionLevel _level;
        protected internal byte[] _workingBuffer;
        protected internal int _bufferSize = ZlibConstants.WorkingBufferSizeDefault;
        private readonly byte[] _buf1 = new byte[1];

        protected internal Stream _stream;
        protected internal CompressionStrategy Strategy = CompressionStrategy.Default;

        // workitem 7159
        private readonly CRC32 crc;
        protected internal string _GzipFileName;
        protected internal string _GzipComment;
        protected internal DateTime _GzipMtime;
        protected internal int _gzipHeaderByteCount;

        private readonly Encoding _encoding;

        internal int Crc32 => crc?.Crc32Result ?? 0;

        public ZlibBaseStream(Stream stream,
                              CompressionMode compressionMode,
                              CompressionLevel level,
                              ZlibStreamFlavor flavor,
                              Encoding encoding)
        {
            _flushMode = FlushType.None;

            //this._workingBuffer = new byte[WORKING_BUFFER_SIZE_DEFAULT];
            _stream = stream;
            _compressionMode = compressionMode;
            _flavor = flavor;
            _level = level;

            _encoding = encoding;

            // workitem 7159
            if (flavor == ZlibStreamFlavor.GZIP)
            {
                crc = new CRC32();
            }
        }

        protected internal bool _wantCompress => (_compressionMode == CompressionMode.Compress);

        private ZlibCodec z
        {
            get
            {
                if (_z is null)
                {
                    bool wantRfc1950Header = (_flavor == ZlibStreamFlavor.ZLIB);
                    _z = new ZlibCodec();
                    if (_compressionMode == CompressionMode.Decompress)
                    {
                        _z.InitializeInflate(wantRfc1950Header);
                    }
                    else
                    {
                        _z.Strategy = Strategy;
                        _z.InitializeDeflate(_level, wantRfc1950Header);
                    }
                }
                return _z;
            }
        }

        private byte[] workingBuffer => _workingBuffer ??= new byte[_bufferSize];

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // workitem 7159
            // calculate the CRC on the unccompressed data  (before writing)
            crc?.SlurpBlock(buffer, offset, count);

            if (_streamMode == StreamMode.Undefined)
            {
                _streamMode = StreamMode.Writer;
            }
            else if (_streamMode != StreamMode.Writer)
            {
                throw new ZlibException("Cannot Write after Reading.");
            }

            if (count == 0)
            {
                return;
            }

            // first reference of z property will initialize the private var _z
            z.InputBuffer = buffer;
            _z.NextIn = offset;
            _z.AvailableBytesIn = count;
            var done = false;
            do
            {
                _z.OutputBuffer = workingBuffer;
                _z.NextOut = 0;
                _z.AvailableBytesOut = _workingBuffer.Length;
                int rc = (_wantCompress)
                             ? _z.Deflate(_flushMode)
                             : _z.Inflate(_flushMode);
                if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
                {
                    throw new ZlibException((_wantCompress ? "de" : "in") + "flating: " + _z.Message);
                }

                //if (_workingBuffer.Length - _z.AvailableBytesOut > 0)
                await _stream.WriteAsync(new Memory<byte>(_workingBuffer).Slice(0, _workingBuffer.Length - _z.AvailableBytesOut), cancellationToken);

                done = _z.AvailableBytesIn == 0 && _z.AvailableBytesOut != 0;

                // If GZIP and de-compress, we're done when 8 bytes remain.
                if (_flavor == ZlibStreamFlavor.GZIP && !_wantCompress)
                {
                    done = (_z.AvailableBytesIn == 8 && _z.AvailableBytesOut != 0);
                }
            }
            while (!done);
        }

        private async Task FinishAsync()
        {
            if (_z is null)
            {
                return;
            }

            if (_streamMode == StreamMode.Writer)
            {
                var done = false;
                do
                {
                    _z.OutputBuffer = workingBuffer;
                    _z.NextOut = 0;
                    _z.AvailableBytesOut = _workingBuffer.Length;
                    int rc = (_wantCompress)
                                 ? _z.Deflate(FlushType.Finish)
                                 : _z.Inflate(FlushType.Finish);

                    if (rc != ZlibConstants.Z_STREAM_END && rc != ZlibConstants.Z_OK)
                    {
                        string verb = (_wantCompress ? "de" : "in") + "flating";
                        if (_z.Message is null)
                        {
                            throw new ZlibException($"{verb}: (rc = {rc})");
                        }
                        throw new ZlibException(verb + ": " + _z.Message);
                    }

                    if (_workingBuffer.Length - _z.AvailableBytesOut > 0)
                    {
                        await _stream.WriteAsync(_workingBuffer, 0, _workingBuffer.Length - _z.AvailableBytesOut);
                    }

                    done = _z.AvailableBytesIn == 0 && _z.AvailableBytesOut != 0;

                    // If GZIP and de-compress, we're done when 8 bytes remain.
                    if (_flavor == ZlibStreamFlavor.GZIP && !_wantCompress)
                    {
                        done = (_z.AvailableBytesIn == 8 && _z.AvailableBytesOut != 0);
                    }
                }
                while (!done);

                await FlushAsync();

                // workitem 7159
                if (_flavor == ZlibStreamFlavor.GZIP)
                {
                    if (_wantCompress)
                    {
                        // Emit the GZIP trailer: CRC32 and  size mod 2^32
                        using var intBuf = MemoryPool<byte>.Shared.Rent(4);
                        BinaryPrimitives.WriteInt32LittleEndian(intBuf.Memory.Span, crc.Crc32Result);
                        await _stream.WriteAsync(intBuf.Memory.Slice(0,4));
                        var c2 = (int)(crc.TotalBytesRead & 0x00000000FFFFFFFF);
                        BinaryPrimitives.WriteInt32LittleEndian(intBuf.Memory.Span, c2);
                        await _stream.WriteAsync(intBuf.Memory.Slice(0,4));
                    }
                    else
                    {
                        throw new ZlibException("Writing with decompression is not supported.");
                    }
                }
            }

            // workitem 7159
            else if (_streamMode == StreamMode.Reader)
            {
                if (_flavor == ZlibStreamFlavor.GZIP)
                {
                    if (!_wantCompress)
                    {
                        // workitem 8501: handle edge case (decompress empty stream)
                        if (_z.TotalBytesOut == 0L)
                        {
                            return;
                        }

                        // Read and potentially verify the GZIP trailer: CRC32 and  size mod 2^32
                        var trailer = new byte[8];

                        // workitem 8679
                        if (_z.AvailableBytesIn != 8)
                        {
                            // Make sure we have read to the end of the stream
                            _z.InputBuffer.AsSpan(_z.NextIn, _z.AvailableBytesIn).CopyTo(trailer);
                            int bytesNeeded = 8 - _z.AvailableBytesIn;
                            int bytesRead = await _stream.ReadAsync(trailer,
                                                                    _z.AvailableBytesIn,
                                                                    bytesNeeded);
                            if (bytesNeeded != bytesRead)
                            {
                                throw new ZlibException($"Protocol error. AvailableBytesIn={_z.AvailableBytesIn + bytesRead}, expected 8");
                            }
                        }
                        else
                        {
                            _z.InputBuffer.AsSpan(_z.NextIn, trailer.Length).CopyTo(trailer);
                        }

                        Int32 crc32_expected = BinaryPrimitives.ReadInt32LittleEndian(trailer);
                        Int32 crc32_actual = crc.Crc32Result;
                        Int32 isize_expected = BinaryPrimitives.ReadInt32LittleEndian(trailer.AsSpan(4));
                        var isize_actual = (Int32)(_z.TotalBytesOut & 0x00000000FFFFFFFF);

                        if (crc32_actual != crc32_expected)
                        {
                            throw new ZlibException(
                                                    $"Bad CRC32 in GZIP stream. (actual({crc32_actual:X8})!=expected({crc32_expected:X8}))");
                        }

                        if (isize_actual != isize_expected)
                        {
                            throw new ZlibException(
                                                    $"Bad size in GZIP stream. (actual({isize_actual})!=expected({isize_expected}))");
                        }
                    }
                    else
                    {
                        throw new ZlibException("Reading with compression is not supported.");
                    }
                }
            }
        }

        private void End()
        {
            if (z is null)
            {
                return;
            }
            if (_wantCompress)
            {
                _z.EndDeflate();
            }
            else
            {
                _z.EndInflate();
            }
            _z = null;
        }

        public override async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            if (_stream is null)
            {
                return;
            }
            try
            {
                await FinishAsync();
            }
            finally
            {
                End();
                if (_stream is not null)
                {
                    await _stream.DisposeAsync();
                }
                _stream = null;
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _stream.FlushAsync(cancellationToken);
        }

        public override Int64 Seek(Int64 offset, SeekOrigin origin)
        {
            throw new NotSupportedException();

            //_outStream.Seek(offset, origin);
        }

        public override void SetLength(Int64 value)
        {
            _stream.SetLength(value);
        }

/*
        public int Read()
        {
            if (Read(_buf1, 0, 1) == 0)
                return 0;
            // calculate CRC after reading
            if (crc!=null)
                crc.SlurpBlock(_buf1,0,1);
            return (_buf1[0] & 0xFF);
        }
*/

        private bool _nomoreinput;
        private bool _isDisposed;

        private async Task<string> ReadZeroTerminatedStringAsync()
        {
            var list = new List<byte>();
            var done = false;
            do
            {
                // workitem 7740
                int n = await _stream.ReadAsync(_buf1, 0, 1);
                if (n != 1)
                {
                    throw new ZlibException("Unexpected EOF reading GZIP header.");
                }
                if (_buf1[0] == 0)
                {
                    done = true;
                }
                else
                {
                    list.Add(_buf1[0]);
                }
            }
            while (!done);
            byte[] buffer = list.ToArray();
            return _encoding.GetString(buffer, 0, buffer.Length);
        }

        private async Task<int> ReadAndValidateGzipHeaderAsync(CancellationToken cancellationToken)
        {
            var totalBytesRead = 0;

            // read the header on the first read
            using var rented = MemoryPool<byte>.Shared.Rent(10);
            int n = await _stream.ReadAsync(rented.Memory.Slice(0,10), cancellationToken);
            var header = rented.Memory;

            // workitem 8501: handle edge case (decompress empty stream)
            if (n == 0)
            {
                return 0;
            }

            if (n != 10)
            {
                throw new ZlibException("Not a valid GZIP stream.");
            }

            if (header.Span[0] != 0x1F || header.Span[1] != 0x8B || header.Span[2] != 8)
            {
                throw new ZlibException("Bad GZIP header.");
            }

            int timet = BinaryPrimitives.ReadInt32LittleEndian(header.Span.Slice(4));
            _GzipMtime = TarHeader.EPOCH.AddSeconds(timet);
            totalBytesRead += n;
            if ((header.Span[3] & 0x04) == 0x04)
            {
                // read and discard extra field
                n = await _stream.ReadAsync(header.Slice(0, 2), cancellationToken); // 2-byte length field
                totalBytesRead += n;

                var extraLength = (short)(header.Span[0] + header.Span[1] * 256);
                using var extra = MemoryPool<byte>.Shared.Rent(extraLength);
                n = await _stream.ReadAsync(extra.Memory.Slice(0, extraLength), cancellationToken);
                if (n != extraLength)
                {
                    throw new ZlibException("Unexpected end-of-file reading GZIP header.");
                }
                totalBytesRead += n;
            }
            if ((header.Span[3] & 0x08) == 0x08)
            {
                _GzipFileName = await ReadZeroTerminatedStringAsync();
            }
            if ((header.Span[3] & 0x10) == 0x010)
            {
                _GzipComment = await ReadZeroTerminatedStringAsync();
            }
            if ((header.Span[3] & 0x02) == 0x02)
            {
                await ReadAsync(_buf1, 0, 1, cancellationToken); // CRC16, ignore
            }

            return totalBytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // According to MS documentation, any implementation of the IO.Stream.Read function must:
            // (a) throw an exception if offset & count reference an invalid part of the buffer,
            //     or if count < 0, or if buffer is null
            // (b) return 0 only upon EOF, or if count = 0
            // (c) if not EOF, then return at least 1 byte, up to <count> bytes

            if (_streamMode == StreamMode.Undefined)
            {
                if (!_stream.CanRead)
                {
                    throw new ZlibException("The stream is not readable.");
                }

                // for the first read, set up some controls.
                _streamMode = StreamMode.Reader;

                // (The first reference to _z goes through the private accessor which
                // may initialize it.)
                z.AvailableBytesIn = 0;
                if (_flavor == ZlibStreamFlavor.GZIP)
                {
                    _gzipHeaderByteCount = await ReadAndValidateGzipHeaderAsync(cancellationToken);

                    // workitem 8501: handle edge case (decompress empty stream)
                    if (_gzipHeaderByteCount == 0)
                    {
                        return 0;
                    }
                }
            }

            if (_streamMode != StreamMode.Reader)
            {
                throw new ZlibException("Cannot Read after Writing.");
            }

            if (count == 0)
            {
                return 0;
            }
            if (_nomoreinput && _wantCompress)
            {
                return 0; // workitem 8557
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

            var rc = 0;

            // set up the output of the deflate/inflate codec:
            _z.OutputBuffer = buffer;
            _z.NextOut = offset;
            _z.AvailableBytesOut = count;

            // This is necessary in case _workingBuffer has been resized. (new byte[])
            // (The first reference to _workingBuffer goes through the private accessor which
            // may initialize it.)
            _z.InputBuffer = workingBuffer;

            do
            {
                // need data in _workingBuffer in order to deflate/inflate.  Here, we check if we have any.
                if ((_z.AvailableBytesIn == 0) && (!_nomoreinput))
                {
                    // No data available, so try to Read data from the captive stream.
                    _z.NextIn = 0;
                    _z.AvailableBytesIn = await _stream.ReadAsync(_workingBuffer, 0, _workingBuffer.Length, cancellationToken);
                    if (_z.AvailableBytesIn == 0)
                    {
                        _nomoreinput = true;
                    }
                }

                // we have data in InputBuffer; now compress or decompress as appropriate
                rc = (_wantCompress)
                         ? _z.Deflate(_flushMode)
                         : _z.Inflate(_flushMode);

                if (_nomoreinput && (rc == ZlibConstants.Z_BUF_ERROR))
                {
                    return 0;
                }

                if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
                {
                    throw new ZlibException($"{(_wantCompress ? "de" : "in")}flating:  rc={rc}  msg={_z.Message}");
                }

                if ((_nomoreinput || rc == ZlibConstants.Z_STREAM_END) && (_z.AvailableBytesOut == count))
                {
                    break; // nothing more to read
                }
            } //while (_z.AvailableBytesOut == count && rc == ZlibConstants.Z_OK);
            while (_z.AvailableBytesOut > 0 && !_nomoreinput && rc == ZlibConstants.Z_OK);

            // workitem 8557
            // is there more room in output?
            if (_z.AvailableBytesOut > 0)
            {
                if (rc == ZlibConstants.Z_OK && _z.AvailableBytesIn == 0)
                {
                    // deferred
                }

                // are we completely done reading?
                if (_nomoreinput)
                {
                    // and in compression?
                    if (_wantCompress)
                    {
                        // no more input data available; therefore we flush to
                        // try to complete the read
                        rc = _z.Deflate(FlushType.Finish);

                        if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
                        {
                            throw new ZlibException($"Deflating:  rc={rc}  msg={_z.Message}");
                        }
                    }
                }
            }

            rc = (count - _z.AvailableBytesOut);

            // calculate CRC after reading
            crc?.SlurpBlock(buffer, offset, rc);

            return rc;
        }

        public override Boolean CanRead => _stream.CanRead;

        public override Boolean CanSeek => _stream.CanSeek;

        public override Boolean CanWrite => _stream.CanWrite;

        public override Int64 Length => _stream.Length;

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        internal enum StreamMode
        {
            Writer,
            Reader,
            Undefined
        }
    }
}
