using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Compressors.RLE90;
using SharpCompress.IO;
using ZstdSharp.Unsafe;

namespace SharpCompress.Compressors.Squeezed
{
    public class SqueezeStream : Stream, IStreamStack
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

        void IStreamStack.SetPostion(long position) { }

        private readonly Stream _stream;
        private readonly int _compressedSize;
        private const int NUMVALS = 257;
        private const int SPEOF = 256;
        private bool _processed = false;

        public SqueezeStream(Stream stream, int compressedSize)
        {
            _stream = stream;
            _compressedSize = compressedSize;
#if DEBUG_STREAMS
            this.DebugConstruct(typeof(SqueezeStream));
#endif
        }

        protected override void Dispose(bool disposing)
        {
#if DEBUG_STREAMS
            this.DebugDispose(typeof(SqueezeStream));
#endif
            base.Dispose(disposing);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => _stream.Position;
            set => throw new NotImplementedException();
        }

        public override void Flush() => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_processed)
            {
                return 0;
            }
            _processed = true;
            using var binaryReader = new BinaryReader(_stream);

            // Read numnodes (equivalent to convert_u16!(numnodes, buf))
            var numnodes = binaryReader.ReadUInt16();

            // Validation: numnodes should be within bounds
            if (numnodes >= NUMVALS)
            {
                throw new InvalidDataException(
                    $"Invalid number of nodes {numnodes} (max {NUMVALS - 1})"
                );
            }

            // Handle the case where no nodes exist
            if (numnodes == 0)
            {
                return 0;
            }

            // Build dnode (tree of nodes)
            var dnode = new int[numnodes, 2];
            for (int j = 0; j < numnodes; j++)
            {
                dnode[j, 0] = binaryReader.ReadInt16();
                dnode[j, 1] = binaryReader.ReadInt16();
            }

            // Initialize BitReader for reading bits
            var bitReader = new BitReader(_stream);
            var decoded = new List<byte>();

            int i = 0;
            // Decode the buffer using the dnode tree
            while (true)
            {
                i = dnode[i, bitReader.ReadBit() ? 1 : 0];
                if (i < 0)
                {
                    i = (short)-(i + 1);
                    if (i == SPEOF)
                    {
                        break;
                    }
                    else
                    {
                        decoded.Add((byte)i);
                        i = 0;
                    }
                }
            }

            // Unpack the decoded buffer using the RLE class
            var unpacked = RLE.UnpackRLE(decoded.ToArray());
            unpacked.CopyTo(buffer, 0);
            return unpacked.Count();
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotImplementedException();
    }
}
