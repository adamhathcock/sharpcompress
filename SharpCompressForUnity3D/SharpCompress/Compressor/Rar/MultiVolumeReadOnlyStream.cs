namespace SharpCompress.Compressor.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar.Headers;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using SharpCompress.Common.Rar;

    internal class MultiVolumeReadOnlyStream : Stream
    {
        private long currentEntryTotalReadBytes;
        private long currentPartTotalReadBytes;
        private long currentPosition;
        private Stream currentStream;
        private IEnumerator<RarFilePart> filePartEnumerator;
        private long maxPosition;
        private readonly IExtractionListener streamListener;

        internal MultiVolumeReadOnlyStream(IEnumerable<RarFilePart> parts, IExtractionListener streamListener)
        {
            this.streamListener = streamListener;
            this.filePartEnumerator = parts.GetEnumerator();
            this.filePartEnumerator.MoveNext();
            this.InitializeNextFilePart();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (this.filePartEnumerator != null)
                {
                    this.filePartEnumerator.Dispose();
                    this.filePartEnumerator = null;
                }
                if (this.currentStream != null)
                {
                    this.currentStream.Dispose();
                    this.currentStream = null;
                }
            }
        }

        private bool FileHeader_HasFlag(FileFlags fileFlags1, FileFlags fileFlags2)
        {
            return (( (fileFlags1 & fileFlags2)) == fileFlags2);
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        private void InitializeNextFilePart()
        {
            this.maxPosition = this.filePartEnumerator.Current.FileHeader.CompressedSize;
            this.currentPosition = 0L;
            if (this.currentStream != null)
            {
                this.currentStream.Dispose();
            }
            this.currentStream = this.filePartEnumerator.Current.GetCompressedStream();
            this.currentPartTotalReadBytes = 0L;
            this.streamListener.FireFilePartExtractionBegin(this.filePartEnumerator.Current.FilePartName, this.filePartEnumerator.Current.FileHeader.CompressedSize, this.filePartEnumerator.Current.FileHeader.UncompressedSize);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int num = 0;
            int num2 = offset;
            int num3 = count;
            while (num3 > 0)
            {
                int num4 = num3;
                if (num3 > (this.maxPosition - this.currentPosition))
                {
                    num4 = (int) (this.maxPosition - this.currentPosition);
                }
                int num5 = this.currentStream.Read(buffer, num2, num4);
                if (num5 < 0)
                {
                    throw new EndOfStreamException();
                }
                this.currentPosition += num5;
                num2 += num5;
                num3 -= num5;
                num += num5;
                if (((this.maxPosition - this.currentPosition) != 0L) || !this.FileHeader_HasFlag(this.filePartEnumerator.Current.FileHeader.FileFlags, FileFlags.SPLIT_AFTER))
                {
                    break;
                }
                if (this.filePartEnumerator.Current.FileHeader.Salt != null)
                {
                    throw new InvalidFormatException("Sharpcompress currently does not support multi-volume decryption.");
                }
                string fileName = this.filePartEnumerator.Current.FileHeader.FileName;
                if (!this.filePartEnumerator.MoveNext())
                {
                    throw new InvalidFormatException("Multi-part rar file is incomplete.  Entry expects a new volume: " + fileName);
                }
                this.InitializeNextFilePart();
            }
            this.currentPartTotalReadBytes += num;
            this.currentEntryTotalReadBytes += num;
            this.streamListener.FireCompressedBytesRead(this.currentPartTotalReadBytes, this.currentEntryTotalReadBytes);
            return num;
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
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }
    }
}

