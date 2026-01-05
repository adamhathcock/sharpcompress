using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.IO;

internal class MarkingBinaryReader : BinaryReader
{
    public MarkingBinaryReader(Stream stream)
        : base(stream) { }

    public virtual long CurrentReadByteCount { get; protected set; }

    public virtual void Mark() => CurrentReadByteCount = 0;

    public override int Read() => throw new NotSupportedException();

    public override int Read(byte[] buffer, int index, int count) =>
        throw new NotSupportedException();

    public override int Read(char[] buffer, int index, int count) =>
        throw new NotSupportedException();

    public override bool ReadBoolean() => ReadByte() != 0;

    // NOTE: there is a somewhat fragile dependency on the internals of this class
    // with RarCrcBinaryReader and RarCryptoBinaryReader.
    //
    // RarCrcBinaryReader/RarCryptoBinaryReader need to override any specific methods
    // that call directly to the base BinaryReader and do not delegate to other methods
    // in this class so that it can track the each byte being read.
    //
    // if altering this class in a way that changes the implementation be sure to
    // update RarCrcBinaryReader/RarCryptoBinaryReader.
    public override byte ReadByte()
    {
        CurrentReadByteCount++;
        return base.ReadByte();
    }

    public override byte[] ReadBytes(int count)
    {
        CurrentReadByteCount += count;
        var bytes = base.ReadBytes(count);
        if (bytes.Length != count)
        {
            throw new InvalidFormatException(
                string.Format(
                    "Could not read the requested amount of bytes.  End of stream reached. Requested: {0} Read: {1}",
                    count,
                    bytes.Length
                )
            );
        }
        return bytes;
    }

    public override char ReadChar() => throw new NotSupportedException();

    public override char[] ReadChars(int count) => throw new NotSupportedException();

    public override double ReadDouble() => throw new NotSupportedException();

    public override short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));

    public override int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(4));

    public override long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(ReadBytes(8));

    public override sbyte ReadSByte() => (sbyte)ReadByte();

    public override float ReadSingle() => throw new NotSupportedException();

    public override string ReadString() => throw new NotSupportedException();

    public override ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2));

    public override uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));

    public override ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));

    // RAR5 style variable length encoded value
    // maximum value of 0xffffffffffffffff (64 bits)
    // technote: "implies max 10 bytes consumed" -- but not really because we could extend indefinitely using 0x80 0x80 ... 0x80 00
    //
    // Variable length integer. Can include one or more bytes, where lower 7 bits of every byte contain integer data
    // and highest bit in every byte is the continuation flag. If highest bit is 0, this is the last byte in sequence.
    // So first byte contains 7 least significant bits of integer and continuation flag. Second byte, if present,
    // contains next 7 bits and so on.
    public ulong ReadRarVInt(int maxBytes = 10) =>
        // hopefully this gets inlined
        DoReadRarVInt((maxBytes - 1) * 7);

    private ulong DoReadRarVInt(int maxShift)
    {
        var shift = 0;
        ulong result = 0;
        do
        {
            var b0 = ReadByte();
            var b1 = ((uint)b0) & 0x7f;
            ulong n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                // overflow
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new FormatException("malformed vint");
    }

    public uint ReadRarVIntUInt32(int maxBytes = 5) =>
        // hopefully this gets inlined
        DoReadRarVIntUInt32((maxBytes - 1) * 7);

    public ushort ReadRarVIntUInt16(int maxBytes = 3) =>
        // hopefully this gets inlined
        checked((ushort)DoReadRarVIntUInt32((maxBytes - 1) * 7));

    public byte ReadRarVIntByte(int maxBytes = 2) =>
        // hopefully this gets inlined
        checked((byte)DoReadRarVIntUInt32((maxBytes - 1) * 7));

    private uint DoReadRarVIntUInt32(int maxShift)
    {
        var shift = 0;
        uint result = 0;
        do
        {
            var b0 = ReadByte();
            var b1 = ((uint)b0) & 0x7f;
            var n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                // overflow
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new FormatException("malformed vint");
    }

    // Async versions of read methods
    public virtual async Task<byte> ReadByteAsync(CancellationToken cancellationToken = default)
    {
        CurrentReadByteCount++;
        var buffer = new byte[1];
        var bytesRead = await BaseStream
            .ReadAsync(buffer, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        if (bytesRead != 1)
        {
            throw new EndOfStreamException();
        }
        return buffer[0];
    }

    public virtual async Task<byte[]> ReadBytesAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        CurrentReadByteCount += count;
        var bytes = new byte[count];
        var totalRead = 0;
        while (totalRead < count)
        {
            var bytesRead = await BaseStream
                .ReadAsync(bytes, totalRead, count - totalRead, cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new InvalidFormatException(
                    string.Format(
                        "Could not read the requested amount of bytes.  End of stream reached. Requested: {0} Read: {1}",
                        count,
                        totalRead
                    )
                );
            }
            totalRead += bytesRead;
        }
        return bytes;
    }

    public async Task<bool> ReadBooleanAsync(CancellationToken cancellationToken = default) =>
        await ReadByteAsync(cancellationToken).ConfigureAwait(false) != 0;

    public async Task<short> ReadInt16Async(CancellationToken cancellationToken = default) =>
        BinaryPrimitives.ReadInt16LittleEndian(
            await ReadBytesAsync(2, cancellationToken).ConfigureAwait(false)
        );

    public async Task<int> ReadInt32Async(CancellationToken cancellationToken = default) =>
        BinaryPrimitives.ReadInt32LittleEndian(
            await ReadBytesAsync(4, cancellationToken).ConfigureAwait(false)
        );

    public async Task<long> ReadInt64Async(CancellationToken cancellationToken = default) =>
        BinaryPrimitives.ReadInt64LittleEndian(
            await ReadBytesAsync(8, cancellationToken).ConfigureAwait(false)
        );

    public async Task<sbyte> ReadSByteAsync(CancellationToken cancellationToken = default) =>
        (sbyte)await ReadByteAsync(cancellationToken).ConfigureAwait(false);

    public async Task<ushort> ReadUInt16Async(CancellationToken cancellationToken = default) =>
        BinaryPrimitives.ReadUInt16LittleEndian(
            await ReadBytesAsync(2, cancellationToken).ConfigureAwait(false)
        );

    public async Task<uint> ReadUInt32Async(CancellationToken cancellationToken = default) =>
        BinaryPrimitives.ReadUInt32LittleEndian(
            await ReadBytesAsync(4, cancellationToken).ConfigureAwait(false)
        );

    public async Task<ulong> ReadUInt64Async(CancellationToken cancellationToken = default) =>
        BinaryPrimitives.ReadUInt64LittleEndian(
            await ReadBytesAsync(8, cancellationToken).ConfigureAwait(false)
        );

    public Task<ulong> ReadRarVIntAsync(
        int maxBytes = 10,
        CancellationToken cancellationToken = default
    ) => DoReadRarVIntAsync((maxBytes - 1) * 7, cancellationToken);

    private async Task<ulong> DoReadRarVIntAsync(int maxShift, CancellationToken cancellationToken)
    {
        var shift = 0;
        ulong result = 0;
        do
        {
            var b0 = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            var b1 = ((uint)b0) & 0x7f;
            ulong n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                // overflow
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new FormatException("malformed vint");
    }

    public Task<uint> ReadRarVIntUInt32Async(
        int maxBytes = 5,
        CancellationToken cancellationToken = default
    ) => DoReadRarVIntUInt32Async((maxBytes - 1) * 7, cancellationToken);

    public async Task<ushort> ReadRarVIntUInt16Async(
        int maxBytes = 3,
        CancellationToken cancellationToken = default
    ) =>
        checked(
            (ushort)
                await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, cancellationToken)
                    .ConfigureAwait(false)
        );

    public async Task<byte> ReadRarVIntByteAsync(
        int maxBytes = 2,
        CancellationToken cancellationToken = default
    ) =>
        checked(
            (byte)
                await DoReadRarVIntUInt32Async((maxBytes - 1) * 7, cancellationToken)
                    .ConfigureAwait(false)
        );

    private async Task<uint> DoReadRarVIntUInt32Async(
        int maxShift,
        CancellationToken cancellationToken
    )
    {
        var shift = 0;
        uint result = 0;
        do
        {
            var b0 = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            var b1 = ((uint)b0) & 0x7f;
            var n = b1;
            var shifted = n << shift;
            if (n != shifted >> shift)
            {
                // overflow
                break;
            }
            result |= shifted;
            if (b0 == b1)
            {
                return result;
            }
            shift += 7;
        } while (shift <= maxShift);

        throw new FormatException("malformed vint");
    }
}
