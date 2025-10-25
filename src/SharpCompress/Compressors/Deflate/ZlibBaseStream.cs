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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Deflate;

internal enum ZlibStreamFlavor
{
    ZLIB = 1950,
    DEFLATE = 1951,
    GZIP = 1952,
}

internal class ZlibBaseStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _stream;

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

    protected internal ZlibCodec _z; // deferred init... new ZlibCodec();

    protected internal StreamMode _streamMode = StreamMode.Undefined;
    protected internal FlushType _flushMode;
    protected internal ZlibStreamFlavor _flavor;
    protected internal CompressionMode _compressionMode;
    protected internal CompressionLevel _level;
    protected internal byte[] _workingBuffer;
    protected internal int _bufferSize = ZlibConstants.WorkingBufferSizeDefault;
    protected internal byte[] _buf1 = new byte[1];

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

    public ZlibBaseStream(
        Stream stream,
        CompressionMode compressionMode,
        CompressionLevel level,
        ZlibStreamFlavor flavor,
        Encoding encoding
    )
    {
        _flushMode = FlushType.None;

        //this._workingBuffer = new byte[WORKING_BUFFER_SIZE_DEFAULT];
        _stream = stream;
        _compressionMode = compressionMode;
        _flavor = flavor;
        _level = level;

        _encoding = encoding;

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(ZlibBaseStream));
#endif

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
                var wantRfc1950Header = (_flavor == ZlibStreamFlavor.ZLIB);
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

    public override void Write(byte[] buffer, int offset, int count)
    {
        // workitem 7159
        // calculate the CRC on the unccompressed data  (before writing)
        if (crc != null)
        {
            crc.SlurpBlock(buffer, offset, count);
        }

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
            var rc = (_wantCompress) ? _z.Deflate(_flushMode) : _z.Inflate(_flushMode);
            if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
            {
                throw new ZlibException((_wantCompress ? "de" : "in") + "flating: " + _z.Message);
            }

            //if (_workingBuffer.Length - _z.AvailableBytesOut > 0)
            _stream.Write(_workingBuffer, 0, _workingBuffer.Length - _z.AvailableBytesOut);

            done = _z.AvailableBytesIn == 0 && _z.AvailableBytesOut != 0;

            // If GZIP and de-compress, we're done when 8 bytes remain.
            if (_flavor == ZlibStreamFlavor.GZIP && !_wantCompress)
            {
                done = (_z.AvailableBytesIn == 8 && _z.AvailableBytesOut != 0);
            }
        } while (!done);
    }

#if !NETSTANDARD2_0 && !NETFRAMEWORK
    private async ValueTask FinishAsync(CancellationToken cancellationToken)
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
                var rc =
                    (_wantCompress) ? _z.Deflate(FlushType.Finish) : _z.Inflate(FlushType.Finish);

                if (rc != ZlibConstants.Z_STREAM_END && rc != ZlibConstants.Z_OK)
                {
                    var verb = (_wantCompress ? "de" : "in") + "flating";
                    if (_z.Message is null)
                    {
                        throw new ZlibException(String.Format("{0}: (rc = {1})", verb, rc));
                    }
                    throw new ZlibException(verb + ": " + _z.Message);
                }

                if (_workingBuffer.Length - _z.AvailableBytesOut > 0)
                {
                    _stream.Write(_workingBuffer, 0, _workingBuffer.Length - _z.AvailableBytesOut);
                }

                done = _z.AvailableBytesIn == 0 && _z.AvailableBytesOut != 0;

                // If GZIP and de-compress, we're done when 8 bytes remain.
                if (_flavor == ZlibStreamFlavor.GZIP && !_wantCompress)
                {
                    done = (_z.AvailableBytesIn == 8 && _z.AvailableBytesOut != 0);
                }
            } while (!done);

            Flush();

            // workitem 7159
            if (_flavor == ZlibStreamFlavor.GZIP)
            {
                if (_wantCompress)
                {
                    // Emit the GZIP trailer: CRC32 and  size mod 2^32
                    using var intBufOwner = MemoryPool<byte>.Shared.Rent(4);
                    var intBuf = intBufOwner.Memory.Slice(0, 4);
                    BinaryPrimitives.WriteInt32LittleEndian(intBuf.Span, crc.Crc32Result);
                    await _stream.WriteAsync(intBuf, cancellationToken);
                    var c2 = (int)(crc.TotalBytesRead & 0x00000000FFFFFFFF);
                    BinaryPrimitives.WriteInt32LittleEndian(intBuf.Span, c2);
                    await _stream.WriteAsync(intBuf, cancellationToken);
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
                    using var trailerOwner = MemoryPool<byte>.Shared.Rent(8);
                    var trailer = trailerOwner.Memory.Slice(0, 8);

                    // workitem 8679
                    if (_z.AvailableBytesIn != 8)
                    {
                        // Make sure we have read to the end of the stream
                        _z.InputBuffer.AsSpan(_z.NextIn, _z.AvailableBytesIn).CopyTo(trailer.Span);
                        var bytesNeeded = 8 - _z.AvailableBytesIn;
                        var bytesRead = await _stream.ReadAsync(
                            trailer.Slice(_z.AvailableBytesIn, bytesNeeded), cancellationToken
                        );
                        if (bytesNeeded != bytesRead)
                        {
                            throw new ZlibException(
                                String.Format(
                                    "Protocol error. AvailableBytesIn={0}, expected 8",
                                    _z.AvailableBytesIn + bytesRead
                                )
                            );
                        }
                    }
                    else
                    {
                        _z.InputBuffer.AsSpan(_z.NextIn, trailer.Length).CopyTo(trailer.Span);
                    }

                    var crc32_expected = BinaryPrimitives.ReadInt32LittleEndian(trailer.Span);
                    var crc32_actual = crc.Crc32Result;
                    var isize_expected = BinaryPrimitives.ReadInt32LittleEndian(trailer.Span.Slice(4));
                    var isize_actual = (Int32)(_z.TotalBytesOut & 0x00000000FFFFFFFF);

                    if (crc32_actual != crc32_expected)
                    {
                        throw new ZlibException(
                            String.Format(
                                "Bad CRC32 in GZIP stream. (actual({0:X8})!=expected({1:X8}))",
                                crc32_actual,
                                crc32_expected
                            )
                        );
                    }

                    if (isize_actual != isize_expected)
                    {
                        throw new ZlibException(
                            String.Format(
                                "Bad size in GZIP stream. (actual({0})!=expected({1}))",
                                isize_actual,
                                isize_expected
                            )
                        );
                    }
                }
                else
                {
                    throw new ZlibException("Reading with compression is not supported.");
                }
            }
        }
    }
#else

    private void finish()
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
                var rc =
                    (_wantCompress) ? _z.Deflate(FlushType.Finish) : _z.Inflate(FlushType.Finish);

                if (rc != ZlibConstants.Z_STREAM_END && rc != ZlibConstants.Z_OK)
                {
                    var verb = (_wantCompress ? "de" : "in") + "flating";
                    if (_z.Message is null)
                    {
                        throw new ZlibException(String.Format("{0}: (rc = {1})", verb, rc));
                    }
                    throw new ZlibException(verb + ": " + _z.Message);
                }

                if (_workingBuffer.Length - _z.AvailableBytesOut > 0)
                {
                    _stream.Write(_workingBuffer, 0, _workingBuffer.Length - _z.AvailableBytesOut);
                }

                done = _z.AvailableBytesIn == 0 && _z.AvailableBytesOut != 0;

                // If GZIP and de-compress, we're done when 8 bytes remain.
                if (_flavor == ZlibStreamFlavor.GZIP && !_wantCompress)
                {
                    done = (_z.AvailableBytesIn == 8 && _z.AvailableBytesOut != 0);
                }
            } while (!done);

            Flush();

            // workitem 7159
            if (_flavor == ZlibStreamFlavor.GZIP)
            {
                if (_wantCompress)
                {
                    // Emit the GZIP trailer: CRC32 and  size mod 2^32
                    Span<byte> intBuf = stackalloc byte[4];
                    BinaryPrimitives.WriteInt32LittleEndian(intBuf, crc.Crc32Result);
                    _stream.Write(intBuf);
                    var c2 = (int)(crc.TotalBytesRead & 0x00000000FFFFFFFF);
                    BinaryPrimitives.WriteInt32LittleEndian(intBuf, c2);
                    _stream.Write(intBuf);
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
                    Span<byte> trailer = stackalloc byte[8];

                    // workitem 8679
                    if (_z.AvailableBytesIn != 8)
                    {
                        // Make sure we have read to the end of the stream
                        _z.InputBuffer.AsSpan(_z.NextIn, _z.AvailableBytesIn).CopyTo(trailer);
                        var bytesNeeded = 8 - _z.AvailableBytesIn;
                        var bytesRead = _stream.Read(
                            trailer.Slice(_z.AvailableBytesIn, bytesNeeded)
                        );
                        if (bytesNeeded != bytesRead)
                        {
                            throw new ZlibException(
                                String.Format(
                                    "Protocol error. AvailableBytesIn={0}, expected 8",
                                    _z.AvailableBytesIn + bytesRead
                                )
                            );
                        }
                    }
                    else
                    {
                        _z.InputBuffer.AsSpan(_z.NextIn, trailer.Length).CopyTo(trailer);
                    }

                    var crc32_expected = BinaryPrimitives.ReadInt32LittleEndian(trailer);
                    var crc32_actual = crc.Crc32Result;
                    var isize_expected = BinaryPrimitives.ReadInt32LittleEndian(trailer.Slice(4));
                    var isize_actual = (Int32)(_z.TotalBytesOut & 0x00000000FFFFFFFF);

                    if (crc32_actual != crc32_expected)
                    {
                        throw new ZlibException(
                            String.Format(
                                "Bad CRC32 in GZIP stream. (actual({0:X8})!=expected({1:X8}))",
                                crc32_actual,
                                crc32_expected
                            )
                        );
                    }

                    if (isize_actual != isize_expected)
                    {
                        throw new ZlibException(
                            String.Format(
                                "Bad size in GZIP stream. (actual({0})!=expected({1}))",
                                isize_actual,
                                isize_expected
                            )
                        );
                    }
                }
                else
                {
                    throw new ZlibException("Reading with compression is not supported.");
                }
            }
        }
    }
#endif
    private void end()
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

#if !NETSTANDARD2_0 && !NETFRAMEWORK
    public override async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(ZlibBaseStream));
#endif
        await base.DisposeAsync();
        if (_stream is null)
        {
            return;
        }
        try
        {
            await FinishAsync(CancellationToken.None);
        }
        finally
        {
            end();
            _stream?.Dispose();
            _stream = null;
        }
    }


    #else
    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }
        isDisposed = true;
#if DEBUG_STREAMS
        this.DebugDispose(typeof(ZlibBaseStream));
#endif
        base.Dispose(disposing);
        if (disposing)
        {
            if (_stream is null)
            {
                return;
            }
            try
            {
                finish();
            }
            finally
            {
                end();
                _stream?.Dispose();
                _stream = null;
            }
        }
    }

#endif

    public override void Flush()
    {
        _stream.Flush();
        //rewind the buffer
        ((IStreamStack)this).Rewind(z.AvailableBytesIn); //unused
        z.AvailableBytesIn = 0;
    }
    public override Int64 Seek(Int64 offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    //_outStream.Seek(offset, origin);
    public override void SetLength(Int64 value) => _stream.SetLength(value);

#if NOT
    public int Read()
    {
        if (Read(_buf1, 0, 1) == 0)
            return 0;
        // calculate CRC after reading
        if (crc != null)
            crc.SlurpBlock(_buf1, 0, 1);
        return (_buf1[0] & 0xFF);
    }
#endif

    private bool nomoreinput;
    private bool isDisposed;

    private string ReadZeroTerminatedString()
    {
        var list = new List<byte>();
        var done = false;
        do
        {
            // workitem 7740
            var n = _stream.Read(_buf1, 0, 1);
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
        } while (!done);
        var buffer = list.ToArray();
        return _encoding.GetString(buffer, 0, buffer.Length);
    }

    private int _ReadAndValidateGzipHeader()
    {
        var totalBytesRead = 0;

        // read the header on the first read
        Span<byte> header = stackalloc byte[10];
        var n = _stream.Read(header);

        // workitem 8501: handle edge case (decompress empty stream)
        if (n == 0)
        {
            return 0;
        }

        if (n != 10)
        {
            throw new ZlibException("Not a valid GZIP stream.");
        }

        if (header[0] != 0x1F || header[1] != 0x8B || header[2] != 8)
        {
            throw new ZlibException("Bad GZIP header.");
        }

        var timet = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(4));
        _GzipMtime = TarHeader.EPOCH.AddSeconds(timet);
        totalBytesRead += n;
        if ((header[3] & 0x04) == 0x04)
        {
            // read and discard extra field
            n = _stream.Read(header.Slice(0, 2)); // 2-byte length field
            totalBytesRead += n;

            var extraLength = (short)(header[0] + header[1] * 256);
            var extra = new byte[extraLength];
            n = _stream.Read(extra, 0, extra.Length);
            if (n != extraLength)
            {
                throw new ZlibException("Unexpected end-of-file reading GZIP header.");
            }
            totalBytesRead += n;
        }
        if ((header[3] & 0x08) == 0x08)
        {
            _GzipFileName = ReadZeroTerminatedString();
        }
        if ((header[3] & 0x10) == 0x010)
        {
            _GzipComment = ReadZeroTerminatedString();
        }
        if ((header[3] & 0x02) == 0x02)
        {
            Read(_buf1, 0, 1); // CRC16, ignore
        }

        return totalBytesRead;
    }

#if !NETSTANDARD2_0 && !NETFRAMEWORK
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
                _gzipHeaderByteCount = _ReadAndValidateGzipHeader();

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

        var rc = 0;

        // set up the output of the deflate/inflate codec:
        _z.OutputBuffer = buffer;
        _z.NextOut = offset;
        _z.AvailableBytesOut = count;

        if (count == 0)
        {
            return 0;
        }
        if (nomoreinput && _wantCompress)
        {
            // no more input data available; therefore we flush to
            // try to complete the read
            rc = _z.Deflate(FlushType.Finish);

            if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
            {
                throw new ZlibException(
                    String.Format("Deflating:  rc={0}  msg={1}", rc, _z.Message)
                );
            }

            rc = (count - _z.AvailableBytesOut);

            // calculate CRC after reading
            if (crc != null)
            {
                crc.SlurpBlock(buffer, offset, rc);
            }

            return rc;
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

        // This is necessary in case _workingBuffer has been resized. (new byte[])
        // (The first reference to _workingBuffer goes through the private accessor which
        // may initialize it.)
        _z.InputBuffer = workingBuffer;

        do
        {
            // need data in _workingBuffer in order to deflate/inflate.  Here, we check if we have any.
            if ((_z.AvailableBytesIn == 0) && (!nomoreinput))
            {
                // No data available, so try to Read data from the captive stream.
                _z.NextIn = 0;
                _z.AvailableBytesIn = await _stream.ReadAsync(_workingBuffer, 0, _workingBuffer.Length, cancellationToken);
                if (_z.AvailableBytesIn == 0)
                {
                    nomoreinput = true;
                }
            }

            // we have data in InputBuffer; now compress or decompress as appropriate
            rc = (_wantCompress) ? _z.Deflate(_flushMode) : _z.Inflate(_flushMode);

            if (nomoreinput && (rc == ZlibConstants.Z_BUF_ERROR))
            {
                return 0;
            }

            if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
            {
                throw new ZlibException(
                    String.Format(
                        "{0}flating:  rc={1}  msg={2}",
                        (_wantCompress ? "de" : "in"),
                        rc,
                        _z.Message
                    )
                );
            }

            if (
                (nomoreinput || rc == ZlibConstants.Z_STREAM_END) && (_z.AvailableBytesOut == count)
            )
            {
                break; // nothing more to read
            }
        } //while (_z.AvailableBytesOut == count && rc == ZlibConstants.Z_OK);
        while (_z.AvailableBytesOut > 0 && !nomoreinput && rc == ZlibConstants.Z_OK);

        // workitem 8557
        // is there more room in output?
        if (_z.AvailableBytesOut > 0)
        {
            if (rc == ZlibConstants.Z_OK && _z.AvailableBytesIn == 0)
            {
                // deferred
            }

            // are we completely done reading?
            if (nomoreinput)
            {
                // and in compression?
                if (_wantCompress)
                {
                    // no more input data available; therefore we flush to
                    // try to complete the read
                    rc = _z.Deflate(FlushType.Finish);

                    if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
                    {
                        throw new ZlibException(
                            String.Format("Deflating:  rc={0}  msg={1}", rc, _z.Message)
                        );
                    }
                }
            }
        }

        rc = (count - _z.AvailableBytesOut);

        // calculate CRC after reading
        if (crc != null)
        {
            crc.SlurpBlock(buffer, offset, rc);
        }

        if (rc == ZlibConstants.Z_STREAM_END && z.AvailableBytesIn != 0 && !_wantCompress)
        {
            //rewind the buffer
            ((IStreamStack)this).Rewind(z.AvailableBytesIn); //unused
            z.AvailableBytesIn = 0;
        }

        return rc;
    }
    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
     => throw new NotSupportedException("Use ReadAsync instead.");
    #else

    public override Int32 Read(Byte[] buffer, Int32 offset, Int32 count)
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
                _gzipHeaderByteCount = _ReadAndValidateGzipHeader();

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

        var rc = 0;

        // set up the output of the deflate/inflate codec:
        _z.OutputBuffer = buffer;
        _z.NextOut = offset;
        _z.AvailableBytesOut = count;

        if (count == 0)
        {
            return 0;
        }
        if (nomoreinput && _wantCompress)
        {
            // no more input data available; therefore we flush to
            // try to complete the read
            rc = _z.Deflate(FlushType.Finish);

            if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
            {
                throw new ZlibException(
                    String.Format("Deflating:  rc={0}  msg={1}", rc, _z.Message)
                );
            }

            rc = (count - _z.AvailableBytesOut);

            // calculate CRC after reading
            if (crc != null)
            {
                crc.SlurpBlock(buffer, offset, rc);
            }

            return rc;
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

        // This is necessary in case _workingBuffer has been resized. (new byte[])
        // (The first reference to _workingBuffer goes through the private accessor which
        // may initialize it.)
        _z.InputBuffer = workingBuffer;

        do
        {
            // need data in _workingBuffer in order to deflate/inflate.  Here, we check if we have any.
            if ((_z.AvailableBytesIn == 0) && (!nomoreinput))
            {
                // No data available, so try to Read data from the captive stream.
                _z.NextIn = 0;
                _z.AvailableBytesIn = _stream.Read(_workingBuffer, 0, _workingBuffer.Length);
                if (_z.AvailableBytesIn == 0)
                {
                    nomoreinput = true;
                }
            }

            // we have data in InputBuffer; now compress or decompress as appropriate
            rc = (_wantCompress) ? _z.Deflate(_flushMode) : _z.Inflate(_flushMode);

            if (nomoreinput && (rc == ZlibConstants.Z_BUF_ERROR))
            {
                return 0;
            }

            if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
            {
                throw new ZlibException(
                    String.Format(
                        "{0}flating:  rc={1}  msg={2}",
                        (_wantCompress ? "de" : "in"),
                        rc,
                        _z.Message
                    )
                );
            }

            if (
                (nomoreinput || rc == ZlibConstants.Z_STREAM_END) && (_z.AvailableBytesOut == count)
            )
            {
                break; // nothing more to read
            }
        } //while (_z.AvailableBytesOut == count && rc == ZlibConstants.Z_OK);
        while (_z.AvailableBytesOut > 0 && !nomoreinput && rc == ZlibConstants.Z_OK);

        // workitem 8557
        // is there more room in output?
        if (_z.AvailableBytesOut > 0)
        {
            if (rc == ZlibConstants.Z_OK && _z.AvailableBytesIn == 0)
            {
                // deferred
            }

            // are we completely done reading?
            if (nomoreinput)
            {
                // and in compression?
                if (_wantCompress)
                {
                    // no more input data available; therefore we flush to
                    // try to complete the read
                    rc = _z.Deflate(FlushType.Finish);

                    if (rc != ZlibConstants.Z_OK && rc != ZlibConstants.Z_STREAM_END)
                    {
                        throw new ZlibException(
                            String.Format("Deflating:  rc={0}  msg={1}", rc, _z.Message)
                        );
                    }
                }
            }
        }

        rc = (count - _z.AvailableBytesOut);

        // calculate CRC after reading
        if (crc != null)
        {
            crc.SlurpBlock(buffer, offset, rc);
        }

        if (rc == ZlibConstants.Z_STREAM_END && z.AvailableBytesIn != 0 && !_wantCompress)
        {
            //rewind the buffer
            ((IStreamStack)this).Rewind(z.AvailableBytesIn); //unused
            z.AvailableBytesIn = 0;
        }

        return rc;
    }
#endif

    public override Boolean CanRead => _stream.CanRead;

    public override Boolean CanSeek => _stream.CanSeek;

    public override Boolean CanWrite => _stream.CanWrite;

    public override Int64 Length => _stream.Length;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    internal enum StreamMode
    {
        Writer,
        Reader,
        Undefined,
    }
}
