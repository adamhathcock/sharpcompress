#if NETSTANDARD1_3
using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Converters;

namespace SharpCompress.Compressors.Deflate
{
    public class GZipStream : Stream
    {
        private enum Mode
        {
            Unknown,
            Reader,
            Writer
        }

        internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateTime? LastModified { get; set; }
        private byte[] _buf1 = new byte[1];
        
        private System.IO.Compression.DeflateStream BaseStream;
        private bool disposed;
        private Mode mode;


        private string _GzipFileName;
        private string _GzipComment;
        private DateTime _GzipMtime;
        private int _gzipHeaderByteCount;

        public GZipStream(Stream stream, CompressionMode mode)
            : this(stream, mode, CompressionLevel.Default, false)
        {
        }

        public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level)
            : this(stream, mode, level, false)
        {
        }

        public GZipStream(Stream stream, CompressionMode mode, bool leaveOpen)
            : this(stream, mode, CompressionLevel.Default, leaveOpen)
        {
        }

        public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            if (mode == CompressionMode.Decompress)
            {
                BaseStream = new System.IO.Compression.DeflateStream(stream, System.IO.Compression.CompressionMode.Decompress, leaveOpen);
            }
            else
            {
                System.IO.Compression.CompressionLevel l;
                switch (level)
                {
                    case CompressionLevel.BestSpeed:
                    {
                        l = System.IO.Compression.CompressionLevel.Fastest;
                        break;
                    }
                    case CompressionLevel.None:
                    {
                        l = System.IO.Compression.CompressionLevel.NoCompression;
                        break;
                    }
                    default:
                    {
                        l = System.IO.Compression.CompressionLevel.Optimal;
                        break;
                    }
                }

                BaseStream = new System.IO.Compression.DeflateStream(stream, l, leaveOpen);


            }
        }

        #region Stream methods

        /// <summary>
        /// Indicates whether the stream can be read.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports reading.
        /// </remarks>
        public override bool CanRead
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                return BaseStream.CanRead;
            }
        }

        /// <summary>
        /// Indicates whether the stream supports Seek operations.
        /// </summary>
        /// <remarks>
        /// Always returns false.
        /// </remarks>
        public override bool CanSeek => false;

        /// <summary>
        /// Indicates whether the stream can be written.
        /// </summary>
        /// <remarks>
        /// The return value depends on whether the captive stream supports writing.
        /// </remarks>
        public override bool CanWrite
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                return BaseStream.CanWrite;
            }
        }

        /// <summary>
        /// Reading this property always throws a <see cref="NotImplementedException"/>.
        /// </summary>
        public override long Length => throw new NotSupportedException();

        /// <summary>
        ///   The position of the stream pointer.
        /// </summary>
        ///
        /// <remarks>
        ///   Setting this property always throws a <see
        ///   cref="NotImplementedException"/>. Reading will return the total bytes
        ///   written out, if used in writing, or the total bytes read in, if used in
        ///   reading.  The count may refer to compressed bytes or uncompressed bytes,
        ///   depending on how you've used the stream.
        /// </remarks>
        public override long Position
        {
            get
            {
                switch (mode)
                {
                    case Mode.Writer:
                        return BaseStream.Position + _gzipHeaderByteCount;
                    case Mode.Reader:
                        return BaseStream.Position + _gzipHeaderByteCount;
                    default:
                        return 0;
                }
            }

            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Dispose the stream.
        /// </summary>
        /// <remarks>
        /// This may or may not result in a <c>Close()</c> call on the captive stream.
        /// See the doc on constructors that take a <c>leaveOpen</c> parameter for more information.
        /// </remarks>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposed)
                {
                    if (disposing && (BaseStream != null))
                    {
                        BaseStream.Dispose();;
                    }
                    disposed = true;
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        /// <summary>
        /// Flush the stream.
        /// </summary>
        public override void Flush()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("GZipStream");
            }
            BaseStream.Flush();
        }

        /// <summary>
        ///   Read and decompress data from the source stream.
        /// </summary>
        ///
        /// <remarks>
        ///   With a <c>GZipStream</c>, decompression is done through reading.
        /// </remarks>
        ///
        /// <example>
        /// <code>
        /// byte[] working = new byte[WORKING_BUFFER_SIZE];
        /// using (System.IO.Stream input = System.IO.File.OpenRead(_CompressedFile))
        /// {
        ///     using (Stream decompressor= new Ionic.Zlib.GZipStream(input, CompressionMode.Decompress, true))
        ///     {
        ///         using (var output = System.IO.File.Create(_DecompressedFile))
        ///         {
        ///             int n;
        ///             while ((n= decompressor.Read(working, 0, working.Length)) !=0)
        ///             {
        ///                 output.Write(working, 0, n);
        ///             }
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="buffer">The buffer into which the decompressed data should be placed.</param>
        /// <param name="offset">the offset within that data array to put the first byte read.</param>
        /// <param name="count">the number of bytes to read.</param>
        /// <returns>the number of bytes actually read</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("GZipStream");
            }
            int n = BaseStream.Read(buffer, offset, count);

            // Console.WriteLine("GZipStream::Read(buffer, off({0}), c({1}) = {2}", offset, count, n);
            // Console.WriteLine( Util.FormatByteArray(buffer, offset, n) );

            if (mode == Mode.Unknown)
            {
                _gzipHeaderByteCount = _ReadAndValidateGzipHeader();
                mode = Mode.Reader;
                FileName = _GzipFileName;
                Comment = _GzipComment;
            }
            return n;
        }


        private int _ReadAndValidateGzipHeader()
        {
            int totalBytesRead = 0;

            // read the header on the first read
            byte[] header = new byte[10];
            int n = BaseStream.Read(header, 0, header.Length);

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

            Int32 timet = DataConverter.LittleEndian.GetInt32(header, 4);
            _GzipMtime = UnixEpoch.AddSeconds(timet);
            totalBytesRead += n;
            if ((header[3] & 0x04) == 0x04)
            {
                // read and discard extra field
                n = BaseStream.Read(header, 0, 2); // 2-byte length field
                totalBytesRead += n;

                Int16 extraLength = (Int16)(header[0] + header[1] * 256);
                byte[] extra = new byte[extraLength];
                n = BaseStream.Read(extra, 0, extra.Length);
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

        private string ReadZeroTerminatedString()
        {
            var list = new List<byte>();
            bool done = false;
            do
            {
                // workitem 7740
                int n = BaseStream.Read(_buf1, 0, 1);
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
            byte[] a = list.ToArray();
            return ArchiveEncoding.Default.GetString(a, 0, a.Length);
        }

        /// <summary>
        ///   Calling this method always throws a <see cref="NotImplementedException"/>.
        /// </summary>
        /// <param name="offset">irrelevant; it will always throw!</param>
        /// <param name="origin">irrelevant; it will always throw!</param>
        /// <returns>irrelevant!</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   Calling this method always throws a <see cref="NotImplementedException"/>.
        /// </summary>
        /// <param name="value">irrelevant; this method will always throw!</param>
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   Write data to the stream.
        /// </summary>
        ///
        /// <remarks>
        /// <para>
        ///   If you wish to use the <c>GZipStream</c> to compress data while writing,
        ///   you can create a <c>GZipStream</c> with <c>CompressionMode.Compress</c>, and a
        ///   writable output stream.  Then call <c>Write()</c> on that <c>GZipStream</c>,
        ///   providing uncompressed data as input.  The data sent to the output stream
        ///   will be the compressed form of the data written.
        /// </para>
        ///
        /// <para>
        ///   A <c>GZipStream</c> can be used for <c>Read()</c> or <c>Write()</c>, but not
        ///   both. Writing implies compression.  Reading implies decompression.
        /// </para>
        ///
        /// </remarks>
        /// <param name="buffer">The buffer holding data to write to the stream.</param>
        /// <param name="offset">the offset within that data array to find the first byte to write.</param>
        /// <param name="count">the number of bytes to write.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("GZipStream");
            }
            if (mode == Mode.Unknown)
            {
                // first write in compression, therefore, emit the GZIP header
                _gzipHeaderByteCount = EmitHeader();
                mode = Mode.Writer;
            }

            BaseStream.Write(buffer, offset, count);
        }

        #endregion

        public string Comment
        {
            get => _GzipComment;
            set
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                _GzipComment = value;
            }
        }

        public string FileName
        {
            get => _GzipFileName;
            set
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("GZipStream");
                }
                _GzipFileName = value;
                if (_GzipFileName == null)
                {
                    return;
                }
                if (_GzipFileName.IndexOf("/") != -1)
                {
                    _GzipFileName = _GzipFileName.Replace("/", "\\");
                }
                if (_GzipFileName.EndsWith("\\"))
                {
                    throw new InvalidOperationException("Illegal filename");
                }

                var index = _GzipFileName.IndexOf("\\");
                if (index != -1)
                {
                    // trim any leading path
                    int length = _GzipFileName.Length;
                    int num = length;
                    while (--num >= 0)
                    {
                        char c = _GzipFileName[num];
                        if (c == '\\')
                        {
                            _GzipFileName = _GzipFileName.Substring(num + 1, length - num - 1);
                        }
                    }
                }
            }
        }

        private int EmitHeader()
        {
            byte[] commentBytes = (Comment == null) ? null : ArchiveEncoding.Default.GetBytes(Comment);
            byte[] filenameBytes = (FileName == null) ? null : ArchiveEncoding.Default.GetBytes(FileName);

            int cbLength = (Comment == null) ? 0 : commentBytes.Length + 1;
            int fnLength = (FileName == null) ? 0 : filenameBytes.Length + 1;

            int bufferLength = 10 + cbLength + fnLength;
            var header = new byte[bufferLength];
            int i = 0;

            // ID
            header[i++] = 0x1F;
            header[i++] = 0x8B;

            // compression method
            header[i++] = 8;
            byte flag = 0;
            if (Comment != null)
            {
                flag ^= 0x10;
            }
            if (FileName != null)
            {
                flag ^= 0x8;
            }

            // flag
            header[i++] = flag;

            // mtime
            if (!LastModified.HasValue)
            {
                LastModified = DateTime.Now;
            }
            TimeSpan delta = LastModified.Value - UnixEpoch;
            var timet = (Int32)delta.TotalSeconds;
            DataConverter.LittleEndian.PutBytes(header, i, timet);
            i += 4;

            // xflg
            header[i++] = 0; // this field is totally useless

            // OS
            header[i++] = 0xFF; // 0xFF == unspecified

            // extra field length - only if FEXTRA is set, which it is not.
            //header[i++]= 0;
            //header[i++]= 0;

            // filename
            if (fnLength != 0)
            {
                Array.Copy(filenameBytes, 0, header, i, fnLength - 1);
                i += fnLength - 1;
                header[i++] = 0; // terminate
            }

            // comment
            if (cbLength != 0)
            {
                Array.Copy(commentBytes, 0, header, i, cbLength - 1);
                i += cbLength - 1;
                header[i++] = 0; // terminate
            }

            BaseStream.Write(header, 0, header.Length);

            return header.Length; // bytes written
        }
    }
}
#endif