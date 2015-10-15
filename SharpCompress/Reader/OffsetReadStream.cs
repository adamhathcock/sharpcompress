using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SharpCompress.Reader
{
    /// <summary>
    /// 只读偏移流
    /// </summary>
    public class OffsetReadStream : System.IO.Stream, System.IDisposable
    {
        /// <summary>
        /// 原始流的偏移位置
        /// </summary>
        private Int64 _originalPosition;
        /// <summary>
        /// 原始流的长度
        /// </summary>
        private Int64 _originalLength;
        /// <summary>
        /// 原始流
        /// </summary>
        private Stream _innerStream;
        /// <summary>
        /// 当前原始流偏移位置
        /// </summary>
        private Int64 _currentStreamOffset;
        /// <summary>
        /// 偏移流
        /// </summary>
        /// <param name="s"></param>
        /// <param name="offset">the offset with stream s</param>
        public OffsetReadStream(Stream s, long offset)
            : base() {
            _originalPosition = offset;//s.Position;
            _originalLength = s.Length;
            s.Position = offset;
            _innerStream = s;
            _currentStreamOffset = 0L;
        }

        public override int Read(byte[] buffer, int offset, int count) {
            return _innerStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotImplementedException();
        }

        public override bool CanRead {
            get { return _innerStream.CanRead; }
        }

        public override bool CanSeek {
            get { return _innerStream.CanSeek; }
        }

        public override bool CanWrite {
            get { return false; }
        }

        public override void Flush() {
            _innerStream.Flush();
        }

        public override long Length {
            get {
                return _innerStream.Length - _originalPosition;
            }
        }

        public override long Position {
            get {
                _currentStreamOffset = _innerStream.Position;
                return _currentStreamOffset - _originalPosition;
            }
            set {
                _currentStreamOffset = _originalPosition + value;
                _innerStream.Position = _currentStreamOffset;
            }
        }


        public override long Seek(long offset, System.IO.SeekOrigin origin) {
            switch (origin) {
                case SeekOrigin.End:
                    if (offset < 0) {
                        if (offset + this._originalLength - this._originalPosition < 0) {
                            throw new Exception("out Seek offsetStream postion");
                        }
                        long pos = _innerStream.Seek(offset, SeekOrigin.End) - _originalPosition;
                        return pos;
                    }
                    break;
                case SeekOrigin.Current:
                    if (offset < 0) {
                        if (offset + _originalLength < 0) {
                            throw new Exception("out Seek offsetStream postion");
                        }
                    }
                    break;
                case SeekOrigin.Begin:
                    if (offset < 0) {
                        throw new Exception("out Seek offsetStream postion");
                    }
                    break;
            }
            return _innerStream.Seek(_originalPosition + offset, origin) - _originalPosition;
        }


        public override void SetLength(long value) {
            throw new NotImplementedException();
        }

        void IDisposable.Dispose() {
            Close();
        }

        public override void Close() {
            base.Close();
        }

    }
 
}
