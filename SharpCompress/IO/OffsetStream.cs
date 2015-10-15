using System;
using System.Collections.Generic;
using System.IO;
//using System.Linq;
using System.Text;

namespace SharpCompress.IO
{
    /// <summary>
    ///  this is a read-only stream, maybe we should be a StreamReader
    ///  warning: the originalStream cann't chang length when using this stream
    /// </summary>
    public class OffsetStream : System.IO.Stream
    {
        Stream _originalStream;
        long _os_originalOffset;
        long _of_length;
        long _os_originalCurrentPos;
        /// <summary>
        /// 只适合读写 关联流的中指定偏移位置以及长度的一部分数据，
        /// 当前流关闭不会影响关联的流
        /// </summary>
        /// <param name="originalStream"></param>
        /// <param name="offset">原始流的偏移位置(立即设置原始流的postion=offset)</param>
        /// <param name="length">在原始流中的长度</param>
        public OffsetStream(Stream originalStream, long offset, long length) {
            if (originalStream == null) {
                throw new Exception("null source stream for new OffsetStream()");
            }
            this._originalStream = originalStream;
            this._os_originalOffset = offset;
            this._of_length = length;
            this.Position = 0; // reset to beginning (?)
            System.Diagnostics.Debug.Assert(length >= 0 && length <= (originalStream.Length - offset));
            if (length < 0 || length > (originalStream.Length - offset)) {
                throw new Exception("out length with offsetStream.");
            }
        }
        /// <summary>
        /// 只适合读写 关联流的中指定偏移位置以及长度的一部分数据
        /// </summary>
        /// <param name="originalStream"></param>
        /// <param name="offset"></param>
        public OffsetStream(Stream originalStream, long offset)
            : this(originalStream, offset, originalStream.Length - offset) { }

        public override bool CanRead { get { return _originalStream.CanRead; } }
        public override bool CanSeek { get { return _originalStream.CanSeek; } }
        public override bool CanWrite { get { return _originalStream.CanWrite; } }
        public override bool CanTimeout {
            get { return _originalStream.CanTimeout; }
        }
        public override int ReadTimeout {
            get { return _originalStream.ReadTimeout; }
            set { _originalStream.ReadTimeout = value; }
        }

        public override int WriteTimeout {
            get { return _originalStream.WriteTimeout; }
            set { _originalStream.WriteTimeout = value; }
        }

        public override void Flush() { _originalStream.Flush(); }
        /// <summary>
        /// 注意：原始关联流的长度不可减少，否则会造成流位置超出长度的异常
        /// </summary>
        public override long Length {
            get {
                //return  _os_length;
                long newlen = _originalStream.Length - _os_originalOffset;
                if (newlen < _of_length) {
                    throw new Exception("the offset length not allowed on  Less than  origibal input stream length! ");
                }
                return newlen;
            }
        }

        public override long Position {
            get {
                _os_originalCurrentPos = _originalStream.Position;
                long newpos = _os_originalCurrentPos - _os_originalOffset;
                return newpos;
            }
            set {
                _os_originalCurrentPos = _os_originalOffset + value;
                _originalStream.Position = _os_originalCurrentPos;
            }
        }
        /// <summary>
        /// 会修改关联流的长度，谨慎使用
        /// </summary>
        /// <param name="value"></param>
        public override void SetLength(long value) {
            //
            //if (value < 0L) throw new Exception("setlength not allowed on Less than  OffsetStream");
            if ((value - _os_originalOffset) < _of_length) throw new Exception("setlength not allowed on Less than  OffsetStream");
            long newlen = value + _os_originalOffset;
            _originalStream.SetLength(newlen);
        }
        public override long Seek(long offset, SeekOrigin origin) {
            long new_offset = _originalStream.Position + offset; // SeekOrigin.Current

            switch (origin) {
                case SeekOrigin.Begin:
                    new_offset = _os_originalOffset + offset;
                    goto case SeekOrigin.Current;
                case SeekOrigin.End:
                    if (offset > 0) {
                        throw new Exception("OffsetStream.Seek(offset,SeekOrigin.End) outside offset stream limits");
                    }
                    new_offset = _os_originalOffset + _of_length + offset;
                    goto case SeekOrigin.Current;
                case SeekOrigin.Current:
                    if ((new_offset < _os_originalOffset) || (new_offset >= (_os_originalOffset + _of_length))) {
                        throw new Exception(
                            "OffsetStream.Seek() outside offset stream limits" +
                            String.Format(", parms({0},{1})", offset, origin.ToString()) +
                            String.Format(", new_offset({0}) os_offset({1}) os_length({2})", new_offset, _os_originalOffset, _of_length));
                    }
                    return _originalStream.Seek(new_offset, SeekOrigin.Begin) - _os_originalOffset;
                default:
                    throw new Exception("unknown SeekOrigin value" + origin.ToString());
            }
        }

        public override int Read(byte[] buffer, int offset, int count) {
            // TODO: we should rangecheck our current seekpointer against our offsets and count
            int num_read = _originalStream.Read(buffer, offset, count);
            return num_read;
        }
        /// <summary>
        /// 指定长度内的数据写入
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count) {
            _originalStream.Write(buffer, offset, count);
        }

        public bool KeepStreamsOpen { get; private set; }
        protected override void Dispose(bool disposing) {           
            base.Dispose(disposing);
            if (disposing && !KeepStreamsOpen) {
                _originalStream.Dispose();
            }
        }
    }
}
