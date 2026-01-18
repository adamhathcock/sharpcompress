using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar;

internal class AsyncRarCrcBinaryReader : AsyncMarkingBinaryReader
{
    private uint _currentCrc;

    public AsyncRarCrcBinaryReader(Stream stream, CancellationToken ct = default)
        : base(stream, ct) { }

    public uint GetCrc32() => ~_currentCrc;

    public void ResetCrc() => _currentCrc = 0xffffffff;

    protected void UpdateCrc(byte b) => _currentCrc = RarCRC.CheckCrc(_currentCrc, b);

    protected void UpdateCrc(byte[] bytes, int offset, int count) =>
        _currentCrc = RarCRC.CheckCrc(_currentCrc, bytes, offset, count);

    protected async ValueTask<byte[]> ReadBytesNoCrcAsync(int count, CancellationToken ct = default)
    {
        CurrentReadByteCount += count;
        var buffer = new byte[count];
        await BaseStream.ReadExactAsync(buffer, 0, count, ct).ConfigureAwait(false);
        return buffer;
    }

    public override async ValueTask<byte> ReadByteAsync(CancellationToken ct = default)
    {
        var b = await base.ReadByteAsync(ct).ConfigureAwait(false);
        _currentCrc = RarCRC.CheckCrc(_currentCrc, b);
        return b;
    }

    public override async ValueTask<byte[]> ReadBytesAsync(
        int count,
        CancellationToken ct = default
    )
    {
        var result = await base.ReadBytesAsync(count, ct).ConfigureAwait(false);
        _currentCrc = RarCRC.CheckCrc(_currentCrc, result, 0, result.Length);
        return result;
    }
}
