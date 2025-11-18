using System;
using System.Data;
using System.IO;
using System.Linq;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Arj
{
    [CLSCompliant(true)]
    public sealed class LhaStream<C> : Stream, IStreamStack
        where C : ILhaDecoderConfig, new()
    {
        private readonly BitReader _bitReader;
        private readonly Stream _stream;

        private readonly HuffTree _commandTree;
        private readonly HuffTree _offsetTree;
        private int _remainingCommands;
        private (int offset, int count)? _copyProgress;
        private readonly RingBuffer _ringBuffer;
        private readonly C _config = new C();

        private const int NUM_COMMANDS = 510;
        private const int NUM_TEMP_CODELEN = 20;

        private readonly int _originalSize;
        private int _producedBytes = 0;

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

        public LhaStream(Stream compressedStream, int originalSize)
        {
            _stream = compressedStream ?? throw new ArgumentNullException(nameof(compressedStream));
            _bitReader = new BitReader(compressedStream);
            _ringBuffer = _config.RingBuffer;
            _commandTree = new HuffTree(NUM_COMMANDS * 2);
            _offsetTree = new HuffTree(NUM_TEMP_CODELEN * 2);
            _remainingCommands = 0;
            _copyProgress = null;
            _originalSize = originalSize;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || (offset + count) > buffer.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (_producedBytes >= _originalSize)
            {
                return 0; // EOF
            }
            if (count == 0)
            {
                return 0;
            }

            int bytesRead = FillBuffer(buffer);
            return bytesRead;
        }

        private byte ReadCodeLength()
        {
            byte len = (byte)_bitReader.ReadBits(3);
            if (len == 7)
            {
                while (_bitReader.ReadBit() != 0)
                {
                    len++;
                    if (len > 255)
                    {
                        throw new InvalidOperationException("Code length overflow");
                    }
                }
            }
            return len;
        }

        private int ReadCodeSkip(int skipRange)
        {
            int bits;
            int increment;

            switch (skipRange)
            {
                case 0:
                    return 1;
                case 1:
                    bits = 4;
                    increment = 3; // 3..=18
                    break;
                default:
                    bits = 9;
                    increment = 20; // 20..=531
                    break;
            }

            int skip = _bitReader.ReadBits(bits);
            return skip + increment;
        }

        private void ReadTempTree()
        {
            byte[] codeLengths = new byte[NUM_TEMP_CODELEN];

            // number of codes to read (5 bits)
            int numCodes = _bitReader.ReadBits(5);

            // single code only
            if (numCodes == 0)
            {
                int code = _bitReader.ReadBits(5);
                _offsetTree.SetSingle((byte)code);
                return;
            }

            if (numCodes > NUM_TEMP_CODELEN)
            {
                throw new Exception("temporary codelen table has invalid size");
            }

            // read actual lengths
            int count = Math.Min(3, numCodes);
            for (int i = 0; i < count; i++)
            {
                codeLengths[i] = (byte)ReadCodeLength();
            }

            // 2-bit skip value follows
            int skip = _bitReader.ReadBits(2);

            if (3 + skip > numCodes)
            {
                throw new Exception("temporary codelen table has invalid size");
            }

            for (int i = 3 + skip; i < numCodes; i++)
            {
                codeLengths[i] = (byte)ReadCodeLength();
            }

            _offsetTree.BuildTree(codeLengths, numCodes);
        }

        private void ReadCommandTree()
        {
            byte[] codeLengths = new byte[NUM_COMMANDS];

            // number of codes to read (9 bits)
            int numCodes = _bitReader.ReadBits(9);

            // single code only
            if (numCodes == 0)
            {
                int code = _bitReader.ReadBits(9);
                _commandTree.SetSingle((ushort)code);
                return;
            }

            if (numCodes > NUM_COMMANDS)
            {
                throw new Exception("commands codelen table has invalid size");
            }

            int index = 0;
            while (index < numCodes)
            {
                for (int n = 0; n < numCodes - index; n++)
                {
                    int code = _offsetTree.ReadEntry(_bitReader);

                    if (code >= 0 && code <= 2) // skip range
                    {
                        int skipCount = ReadCodeSkip(code);
                        index += n + skipCount;
                        goto outerLoop;
                    }
                    else
                    {
                        codeLengths[index + n] = (byte)(code - 2);
                    }
                }
                break;

                outerLoop:
                ;
            }

            _commandTree.BuildTree(codeLengths, numCodes);
        }

        private void ReadOffsetTree()
        {
            int numCodes = _bitReader.ReadBits(_config.OffsetBits);
            if (numCodes == 0)
            {
                int code = _bitReader.ReadBits(_config.OffsetBits);
                _offsetTree.SetSingle(code);
                return;
            }

            if (numCodes > _config.HistoryBits)
            {
                throw new InvalidDataException("Offset code table too large");
            }

            byte[] codeLengths = new byte[NUM_TEMP_CODELEN];
            for (int i = 0; i < numCodes; i++)
            {
                codeLengths[i] = (byte)ReadCodeLength();
            }

            _offsetTree.BuildTree(codeLengths, numCodes);
        }

        private void BeginNewBlock()
        {
            ReadTempTree();
            ReadCommandTree();
            ReadOffsetTree();
        }

        private int ReadCommand() => _commandTree.ReadEntry(_bitReader);

        private int ReadOffset()
        {
            int bits = _offsetTree.ReadEntry(_bitReader);
            if (bits <= 1)
            {
                return bits;
            }

            int res = _bitReader.ReadBits(bits - 1);
            return res | (1 << (bits - 1));
        }

        private int CopyFromHistory(byte[] target, int targetIndex, int offset, int count)
        {
            var historyIter = _ringBuffer.IterFromOffset(offset);
            int copied = 0;

            while (
                copied < count && historyIter.MoveNext() && (targetIndex + copied) < target.Length
            )
            {
                target[targetIndex + copied] = historyIter.Current;
                copied++;
            }

            if (copied < count)
            {
                _copyProgress = (offset, count - copied);
            }

            return copied;
        }

        public int FillBuffer(byte[] buffer)
        {
            int bufLen = buffer.Length;
            int bufIndex = 0;

            // stop when we reached original size
            if (_producedBytes >= _originalSize)
            {
                return 0;
            }

            // calculate limit, so that we don't go over the original size
            int remaining = (int)Math.Min(bufLen, _originalSize - _producedBytes);

            while (bufIndex < remaining)
            {
                if (_copyProgress.HasValue)
                {
                    var (offset, count) = _copyProgress.Value;
                    int copied = CopyFromHistory(
                        buffer,
                        bufIndex,
                        offset,
                        (int)Math.Min(count, remaining - bufIndex)
                    );
                    bufIndex += copied;
                    _copyProgress = null;
                }

                if (_remainingCommands == 0)
                {
                    _remainingCommands = _bitReader.ReadBits(16);
                    if (bufIndex + _remainingCommands > remaining)
                    {
                        break;
                    }
                    BeginNewBlock();
                }

                _remainingCommands--;

                int command = ReadCommand();

                if (command >= 0 && command <= 0xFF)
                {
                    byte value = (byte)command;
                    buffer[bufIndex++] = value;
                    _ringBuffer.Push(value);
                }
                else
                {
                    int count = command - 0x100 + 3;
                    int offset = ReadOffset();
                    int copyCount = (int)Math.Min(count, remaining - bufIndex);
                    bufIndex += CopyFromHistory(buffer, bufIndex, offset, copyCount);
                }
            }

            _producedBytes += bufIndex;
            return bufIndex;
        }
    }
}
