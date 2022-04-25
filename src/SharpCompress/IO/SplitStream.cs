using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpCompress.IO
{
    public class SplitStream : Stream
    {
        private int _idx;
        private long _size;
        private bool _isSplit;
        private long _prevSize;
        private long _pos;
        private long _streamCount;
        private FileInfo[]? _files;
        private Stream[]? _streams;
        private Stream _stream;


        public SplitStream(IEnumerable<FileInfo> files) : this(null, files)
        {
        }

        public SplitStream(IEnumerable<Stream> streams) : this(streams, null)
        {
        }

        private SplitStream(IEnumerable<Stream>? streams, IEnumerable<FileInfo>? files)
        {
            if (streams != null)
            {
                _streams = streams.ToArray();
                _isSplit = _streams.Length > 1;
                _size = _streams.Sum(a => a.Length);
                _streamCount = _streams.Length;
            }
            else if (files != null)
            {
                _files = files.ToArray();
                _isSplit = _files.Length > 1;
                _size = _files.Sum(a => a.Length);
                _streamCount = _files.Length;
            }
            _idx = 0;
            _prevSize = 0;
            _stream = OpenStream(0);
        }

        private Stream OpenStream(int idx)
        {
            if (_streams != null)
                _stream = _streams[idx];
            else if (_files != null)
            {
                if (_stream != null)
                    _stream.Dispose();

                _stream = File.OpenRead(_files[idx].FullName);
            }

            _idx = idx;
            _pos = 0;
            return _stream;
        }

        private long StreamLen(int idx)
        {
            if (_streams != null)
                return _streams[idx].Length;
            else if (_files != null)
                return _files[idx].Length;
            return 0;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _size;

        public override long Position
        {
            get => _prevSize + _pos;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = (int)Math.Min(count, _size - this.Position);

            if (count <= 0)
                return 0;


            int total = count;
            int r = -1;

            while (count != 0 && r != 0)
            {
                r = _stream.Read(buffer, offset, count);
                _pos += (long)r;
                count -= r;
                offset += r;

                if (_isSplit && _pos == StreamLen(_idx))
                    Seek(0, SeekOrigin.Current); //will load next file
            }

            return total - count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long pos = this.Position;
            switch (origin)
            {
                case SeekOrigin.Begin: pos = offset; break;
                case SeekOrigin.Current: pos += offset; break;
                case SeekOrigin.End: pos = Length + offset; break;
            }

            if (_isSplit)
            {
                _prevSize = 0;
                for (int i = 0; i < _streamCount; i++)
                {
                    if (_prevSize + StreamLen(i) > pos)
                    {
                        if (_idx != i)
                            _stream = OpenStream(i);
                        break;
                    }
                    _prevSize += StreamLen(i);
                }
            }

            _pos = pos - _prevSize;

            if (_pos != _stream.Position && this.Position != this.Length)
                _stream.Seek(_pos, SeekOrigin.Begin);
            return pos;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            try
            {
                if (_stream != null)
                    _stream.Close();
            }
            catch { }
            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (_stream != null)
                    _stream.Dispose();
            }
            catch { }
            base.Dispose(disposing);
        }
    }
}
