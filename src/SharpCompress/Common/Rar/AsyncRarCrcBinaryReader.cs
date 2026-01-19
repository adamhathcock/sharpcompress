using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Rar;

namespace SharpCompress.Common.Rar;

internal class AsyncRarCrcBinaryReader : AsyncMarkingBinaryReader
{
    private uint _currentCrc;

    public AsyncRarCrcBinaryReader(Stream stream)
        : base(stream) { }

    public uint GetCrc32() => ~_currentCrc;

    public void ResetCrc() => _currentCrc = 0xffffffff;

    protected void UpdateCrc(byte b) => _currentCrc = RarCRC.CheckCrc(_currentCrc, b);

    protected async ValueTask<byte[]> ReadBytesNoCrcAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        return await base.ReadBytesAsync(count, cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask<byte> ReadByteAsync(
        CancellationToken cancellationToken = default
    )
    {
        var b = await base.ReadByteAsync(cancellationToken).ConfigureAwait(false);
        _currentCrc = RarCRC.CheckCrc(_currentCrc, b);
        return b;
    }

    public override async ValueTask<byte[]> ReadBytesAsync(
        int count,
        CancellationToken cancellationToken = default
    )
    {
        var result = await base.ReadBytesAsync(count, cancellationToken).ConfigureAwait(false);
        _currentCrc = RarCRC.CheckCrc(_currentCrc, result, 0, result.Length);
        return result;
    }
}
