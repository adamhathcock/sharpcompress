#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;

namespace SharpCompress.Compressors.Rar
{
    internal sealed class MultiVolumeReadOnlyStream : Stream
    {
        private long currentPosition;
        private long maxPosition;

        private IAsyncEnumerator<RarFilePart> filePartEnumerator;
        private Stream currentStream;

        private readonly IExtractionListener streamListener;

        private long currentPartTotalReadBytes;
        private long currentEntryTotalReadBytes;

        internal MultiVolumeReadOnlyStream(IExtractionListener streamListener)
        {
            this.streamListener = streamListener;
        }

        internal async ValueTask Initialize(IAsyncEnumerable<RarFilePart> parts, CancellationToken cancellationToken)
        {
            filePartEnumerator = parts.GetAsyncEnumerator(cancellationToken);
            await filePartEnumerator.MoveNextAsync(cancellationToken);
            await InitializeNextFilePartAsync(cancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            if (filePartEnumerator != null)
            {
                await filePartEnumerator.DisposeAsync();
                filePartEnumerator = null;
            }
            currentStream = null;
        }

        private async ValueTask InitializeNextFilePartAsync(CancellationToken cancellationToken)
        {
            maxPosition = filePartEnumerator.Current.FileHeader.CompressedSize;
            currentPosition = 0;
            currentStream = await filePartEnumerator.Current.GetCompressedStreamAsync(cancellationToken);

            currentPartTotalReadBytes = 0;

            CurrentCrc = filePartEnumerator.Current.FileHeader.FileCrc;

            streamListener.FireFilePartExtractionBegin(filePartEnumerator.Current.FilePartName,
                                                       filePartEnumerator.Current.FileHeader.CompressedSize,
                                                       filePartEnumerator.Current.FileHeader.UncompressedSize);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int totalRead = 0;
            int currentOffset = 0;
            int currentCount = buffer.Length;
            while (currentCount > 0)
            {
                int readSize = currentCount;
                if (currentCount > maxPosition - currentPosition)
                {
                    readSize = (int)(maxPosition - currentPosition);
                }

                int read = await currentStream.ReadAsync(buffer.Slice(currentOffset, readSize), cancellationToken);
                if (read < 0)
                {
                    throw new EndOfStreamException();
                }

                currentPosition += read;
                currentOffset += read;
                currentCount -= read;
                totalRead += read;
                if (((maxPosition - currentPosition) == 0)
                    && filePartEnumerator.Current.FileHeader.IsSplitAfter)
                {
                    if (filePartEnumerator.Current.FileHeader.R4Salt != null)
                    {
                        throw new InvalidFormatException("Sharpcompress currently does not support multi-volume decryption.");
                    }
                    string fileName = filePartEnumerator.Current.FileHeader.FileName;
                    if (!await filePartEnumerator.MoveNextAsync(cancellationToken))
                    {
                        throw new InvalidFormatException(
                                                         "Multi-part rar file is incomplete.  Entry expects a new volume: " + fileName);
                    }
                    await InitializeNextFilePartAsync(cancellationToken);
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

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public uint CurrentCrc { get; private set; }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

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