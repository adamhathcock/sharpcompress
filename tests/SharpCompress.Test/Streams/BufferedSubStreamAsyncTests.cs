using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.IO;
using Xunit;

namespace SharpCompress.Test.Streams;

public class BufferedSubStreamAsyncTests
{
    [Fact]
    public async ValueTask ReadAsyncByteArray_CanceledTokenWithCachedData_ThrowsOperationCanceledException()
    {
        using var source = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        using var stream = new BufferedSubStream(source, origin: 0, bytesToRead: source.Length);
        var buffer = new byte[1];

        Assert.Equal(
            1,
            await stream.ReadAsync(buffer, 0, 1, CancellationToken.None).ConfigureAwait(false)
        );

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            stream.ReadAsync(buffer, 0, 1, cts.Token)
        );
    }

#if !LEGACY_DOTNET
    [Fact]
    public async ValueTask ReadAsyncMemory_CanceledTokenWithCachedData_ThrowsOperationCanceledException()
    {
        using var source = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        using var stream = new BufferedSubStream(source, origin: 0, bytesToRead: source.Length);
        var buffer = new byte[1];

        Assert.Equal(
            1,
            await stream.ReadAsync(buffer, 0, 1, CancellationToken.None).ConfigureAwait(false)
        );

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await stream.ReadAsync(buffer.AsMemory(), cts.Token).ConfigureAwait(false)
        );
    }
#endif
}
