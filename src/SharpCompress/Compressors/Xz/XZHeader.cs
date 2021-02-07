using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz
{
    public class XZHeader
    {
        private readonly Stream _stream;
        private static readonly ReadOnlyMemory<byte> MagicHeader = new(new byte[]{ 0xFD, 0x37, 0x7A, 0x58, 0x5a, 0x00 });

        public CheckType BlockCheckType { get; private set; }
        public int BlockCheckSize => ((((int)BlockCheckType) + 2) / 3) * 4;

        public XZHeader(Stream reader)
        {
            _stream = reader;
        }

        public static async ValueTask<XZHeader> FromStream(Stream stream, CancellationToken cancellationToken = default)
        {
            var header = new XZHeader(new NonDisposingStream(stream));
            await header.Process(cancellationToken);
            return header;
        }

        public async ValueTask Process(CancellationToken cancellationToken = default)
        {
            using var header = MemoryPool<byte>.Shared.Rent(6);
            await _stream.ReadAsync(header.Memory.Slice(0, 6), cancellationToken);
            CheckMagicBytes(header.Memory.Slice(0, 6));
            await ProcessStreamFlags(cancellationToken);
        }

        private async ValueTask ProcessStreamFlags(CancellationToken cancellationToken)
        {
            using var header = MemoryPool<byte>.Shared.Rent(6);
            await _stream.ReadAsync(header.Memory.Slice(0, 2), cancellationToken);

            BlockCheckType = (CheckType)(header.Memory.Span[1] & 0x0F);
            byte futureUse = (byte)(header.Memory.Span[1] & 0xF0);
            if (futureUse != 0 || header.Memory.Span[0] != 0)
            {
                throw new InvalidDataException("Unknown XZ Stream Version");
            }
            
            UInt32 crc = await _stream.ReadLittleEndianUInt32(cancellationToken);
            UInt32 calcCrc = Crc32.Compute(header.Memory.Slice(0, 2));
            if (crc != calcCrc)
            {
                throw new InvalidDataException("Stream header corrupt");
            }
        }

        private void CheckMagicBytes(ReadOnlyMemory<byte> header)
        {
            if (!header.Equals(MagicHeader))
            {
                throw new InvalidDataException("Invalid XZ Stream");
            }
        }
    }
}
