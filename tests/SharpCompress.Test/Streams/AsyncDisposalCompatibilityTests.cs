using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Test.Mocks;
using Xunit;

namespace SharpCompress.Test.Streams;

public class AsyncDisposalCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async ValueTask SharpCompressStream_Forwards_Async_Disposal(bool seekable)
    {
        var source = new AsyncDisposeOnlyStream(seekable);
        var wrapper = SharpCompressStream.Create(source, 128);
        await ((IAsyncDisposable)wrapper).DisposeAsync().ConfigureAwait(false);
        Assert.True(source.AsyncDisposed);
        await ((IAsyncDisposable)wrapper).DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async ValueTask Stream_Disposal_Scope_Uses_Runtime_Async_Disposable()
    {
        var source = new AsyncDisposeOnlyStream(true);
        Stream stream = source;
        await using (stream.DisposeAsyncScope().ConfigureAwait(false)) { }
        Assert.True(source.AsyncDisposed);
    }

    private sealed class AsyncDisposeOnlyStream(bool seekable) : AsyncOnlyStream(new MemoryStream())
    {
        public override bool CanSeek => seekable;
        public bool AsyncDisposed { get; private set; }

        public override ValueTask DisposeAsync()
        {
            AsyncDisposed = true;
            return base.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !AsyncDisposed)
            {
                throw new NotSupportedException("Synchronous disposal is not supported.");
            }
            base.Dispose(disposing);
        }
    }
}
