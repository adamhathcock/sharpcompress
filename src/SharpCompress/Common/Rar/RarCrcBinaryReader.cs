using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar;

internal class RarCrcBinaryReader : MarkingBinaryReader
{
    private uint _currentCrc;

    public RarCrcBinaryReader(Stream stream)
        : base(stream) { }

    public uint GetCrc32() => ~_currentCrc;

    public void ResetCrc() => _currentCrc = 0xffffffff;

    protected void UpdateCrc(byte b) => _currentCrc = RarCRC.CheckCrc(_currentCrc, b);

    protected byte[] ReadBytesNoCrc(int count) => base.ReadBytes(count);

    public override byte ReadByte()
    {
        var b = base.ReadByte();
        _currentCrc = RarCRC.CheckCrc(_currentCrc, b);
        return b;
    }

    public override byte[] ReadBytes(int count)
    {
        var result = base.ReadBytes(count);
        _currentCrc = RarCRC.CheckCrc(_currentCrc, result, 0, result.Length);
        return result;
    }

    // Async versions
    public override async Task<byte> ReadByteAsync(CancellationToken cancellationToken = default)
    {
        var b = await base.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        _currentCrc = RarCRC.CheckCrc(_currentCrc, b);
        return b;
    }

    public override async Task<byte[]> ReadBytesAsync(int count, CancellationToken cancellationToken = default)
    {
        var result = await base.ReadBytesAsync(count, cancellationToken).ConfigureAwait(false);
        _currentCrc = RarCRC.CheckCrc(_currentCrc, result, 0, result.Length);
        return result;
    }

    public async Task<byte[]> ReadBytesNoCrcAsync(int count, CancellationToken cancellationToken = default) =>
        await base.ReadBytesAsync(count, cancellationToken).ConfigureAwait(false);
}
