using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Test.Mocks;

// Exercise legal partial reads through every stream read overload used by the parsers.
internal sealed class ShortReadStream(Stream stream, int maxReadSize = 1, bool seekable = true)
    : TestStream(stream, read: true, write: false, seek: seekable)
{
    public override int Read(byte[] buffer, int offset, int count) =>
        base.Read(buffer, offset, Math.Min(count, maxReadSize));

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => base.ReadAsync(buffer, offset, Math.Min(count, maxReadSize), cancellationToken);

#if !LEGACY_DOTNET
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => base.ReadAsync(buffer.Slice(0, Math.Min(buffer.Length, maxReadSize)), cancellationToken);
#endif
}
