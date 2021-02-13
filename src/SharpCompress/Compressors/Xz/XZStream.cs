using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Xz
{
    [CLSCompliant(false)]
    public sealed class XZStream : XZReadOnlyStream
    {
        public static async ValueTask<bool> IsXZStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                return null != await XZHeader.FromStream(stream, cancellationToken);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AssertBlockCheckTypeIsSupported()
        {
            switch (Header?.BlockCheckType)
            {
                case CheckType.NONE:
                    break;
                case CheckType.CRC32:
                    break;
                case CheckType.CRC64:
                    break;
                case CheckType.SHA256:
                    throw new NotImplementedException();
                default:
                    throw new NotSupportedException("Check Type unknown to this version of decoder.");
            }
        }
        public XZHeader? Header { get; private set; }
        public XZIndex? Index { get; private set; }
        public XZFooter? Footer { get; private set; }
        public bool HeaderIsRead { get; private set; }
        private XZBlock? _currentBlock;

        private bool _endOfStream;

        public XZStream(Stream stream) : base(stream)
        {
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken);
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int bytesRead = 0;
            if (_endOfStream)
            {
                return bytesRead;
            }

            if (!HeaderIsRead)
            {
                await ReadHeader();
            }

            bytesRead = await ReadBlocks(buffer, cancellationToken);
            if (bytesRead < buffer.Length)
            {
                _endOfStream = true;
                ReadIndex();
                ReadFooter();
            }
            return bytesRead;
        }

        private async ValueTask ReadHeader()
        {
            Header = await XZHeader.FromStream(BaseStream);
            AssertBlockCheckTypeIsSupported();
            HeaderIsRead = true;
        }

        private void ReadIndex()
        {
            Index = XZIndex.FromStream(BaseStream, true);
            // TODO veryfy Index
        }

        private void ReadFooter()
        {
            Footer = XZFooter.FromStream(BaseStream);
            // TODO verify footer
        }

        private async ValueTask<int> ReadBlocks(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            int bytesRead = 0;
            if (_currentBlock is null)
            {
                _currentBlock = NextBlock();
            }

            for (; ; )
            {
                try
                {
                    if (bytesRead >= buffer.Length)
                    {
                        break;
                    }

                    int remaining = buffer.Length - bytesRead;
                    int newOffset = bytesRead;
                    int justRead = await _currentBlock.ReadAsync(buffer.Slice(newOffset, remaining), cancellationToken);
                    if (justRead < remaining)
                    {
                        _currentBlock = NextBlock();
                    }

                    bytesRead += justRead;
                }
                catch (XZIndexMarkerReachedException)
                {
                    break;
                }
            }
            return bytesRead;
        }

        private XZBlock NextBlock()
        {
            return new XZBlock(BaseStream, Header!.BlockCheckType, Header!.BlockCheckSize);
        }
    }
}
