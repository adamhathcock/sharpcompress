using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressor.Rar
{
    internal class MultiVolumeReadOnlyStream : Stream
    {
        private long currentPosition;
        private long maxPosition;

        private IEnumerator<RarFilePart> filePartEnumerator;
        private Stream currentStream;

        private readonly IStreamListener streamListener;

        private long currentPartTotalReadBytes;
        private long currentEntryTotalReadBytes;

        internal MultiVolumeReadOnlyStream(IEnumerable<RarFilePart> parts, IStreamListener streamListener)
        {
            this.streamListener = streamListener;

            filePartEnumerator = parts.GetEnumerator();
            filePartEnumerator.MoveNext();
            InitializeNextFilePart();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (filePartEnumerator != null)
                {
                    filePartEnumerator.Dispose();
                    filePartEnumerator = null;
                }
                if (currentStream != null)
                {
                    currentStream.Dispose();
                    currentStream = null;
                }
            }
        }

        private void InitializeNextFilePart()
        {
            maxPosition = filePartEnumerator.Current.FileHeader.CompressedSize;
            currentPosition = 0;
            if (currentStream != null)
            {
                currentStream.Dispose();
            }
            currentStream = filePartEnumerator.Current.GetStream();

            currentPartTotalReadBytes = 0;

            streamListener.FireFilePartExtractionBegin(filePartEnumerator.Current.FilePartName,
                 filePartEnumerator.Current.FileHeader.CompressedSize,
                filePartEnumerator.Current.FileHeader.UncompressedSize);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (count > 0)
            {
                int readSize = count;
                if (count > maxPosition - currentPosition)
                {
                    readSize = (int)(maxPosition - currentPosition);
                }

                int read = currentStream.Read(buffer, offset, readSize);
                if (read < 0)
                {
                    throw new EndOfStreamException();
                }

                currentPosition += read;
                offset += read;
                count -= read;
                totalRead += read;
                if (((maxPosition - currentPosition) == 0)
                    && filePartEnumerator.Current.FileHeader.FileFlags.HasFlag(FileFlags.SPLIT_AFTER))
                {
                    string fileName = filePartEnumerator.Current.FileHeader.FileName;
                    if (!filePartEnumerator.MoveNext())
                    {
                        throw new InvalidFormatException("Multi-part rar file is incomplete.  Entry expects a new volume: " + fileName);
                    }
                    InitializeNextFilePart();
                }
                else
                {
                    break;
                }
            }
            currentPartTotalReadBytes += totalRead;
            currentEntryTotalReadBytes += totalRead;
            streamListener.FireCompressedBytesRead(currentPartTotalReadBytes, currentEntryTotalReadBytes);
            return totalRead;
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

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
