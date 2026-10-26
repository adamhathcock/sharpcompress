using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Xz.Filters;
using Xunit;

namespace SharpCompress.Test.Xz.Filters;

public class BcjAsyncAllocationTests
{
#if !LEGACY_DOTNET && !DEBUG
    // Debug async state machines allocate independently of Task result adapters.
    // This guard measures the Release behavior used by the profiling harness.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ArrayReadsAllocateOnlyOnePublicTaskPerResult(int filterId)
    {
        using var input = new MemoryStream(new byte[1024 * 1024]);
        using var filter = CreateFilter(filterId);
        filter.SetBaseStream(input);
        var buffer = new byte[16];
        Assert.Equal(16, await filter.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None));
        var start = GC.GetAllocatedBytesForCurrentThread();
        var total = 16;
        int read;
        while (
            (read = await filter.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None)) != 0
        )
        {
            total += read;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(1024 * 1024, total);
        // One 72-byte Task per 16-byte result is about 4.5 MiB; two are about 9 MiB.
        Assert.InRange(allocated, 0, 6 * 1024 * 1024);
    }

    private static BlockFilter CreateFilter(int filterId) =>
        filterId switch
        {
            0 => new X86Filter(),
            1 => new ArmFilter(),
            2 => new ArmThumbFilter(),
            3 => new PowerPCFilter(),
            4 => new SparcFilter(),
            5 => new IA64Filter(),
            _ => throw new ArgumentOutOfRangeException(nameof(filterId)),
        };
#endif

    [Fact]
    public async Task ArgumentAndDisposalErrorsRemainFaultedTasks()
    {
        using var input = new MemoryStream(new byte[32]);
        using var filter = new X86Filter();
        filter.SetBaseStream(input);
        var invalid = filter.ReadAsync(new byte[1], 2, 1, CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => invalid);
        Assert.True(invalid.IsFaulted);

        filter.Dispose();
        var disposed = filter.ReadAsync(new byte[1], 0, 1, CancellationToken.None);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => disposed);
        Assert.True(disposed.IsFaulted);
    }

    [Fact]
    public async Task CanceledBufferedReadDoesNotConsumeOutput()
    {
        using var input = new MemoryStream(new byte[32]);
        using var filter = new X86Filter();
        filter.SetBaseStream(input);
        var buffer = new byte[1];
        Assert.Equal(1, await filter.ReadAsync(buffer, 0, 1, CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = filter.ReadAsync(buffer, 0, 1, cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled);
        Assert.True(canceled.IsCanceled);
        var remaining = 0;
        while (await filter.ReadAsync(buffer, 0, 1, CancellationToken.None) != 0)
        {
            remaining++;
        }
        Assert.Equal(31, remaining);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronouslyThrowingCustomBaseStreamPreservesAsyncSemantics(bool canceled)
    {
        using var input = new ThrowingInput(canceled);
        using var filter = new CustomX86Filter();
        filter.SetBaseStream(input);
        var task = filter.ReadAsync(new byte[1], 0, 1, CancellationToken.None);
        if (canceled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(task.IsCanceled);
        }
        else
        {
            await Assert.ThrowsAsync<IOException>(() => task);
            Assert.True(task.IsFaulted);
        }
    }

    private sealed class CustomX86Filter : X86Filter
    {
        public override void SetBaseStream(Stream stream) => BaseStream = stream;
    }

    private sealed class ThrowingInput(bool canceled) : MemoryStream
    {
        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) =>
            throw (
                canceled
                    ? new OperationCanceledException(new CancellationToken(true))
                    : new IOException("Injected synchronous read failure")
            );
    }
}
