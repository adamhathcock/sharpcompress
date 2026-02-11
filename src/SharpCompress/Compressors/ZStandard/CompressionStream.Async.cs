using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

public partial class CompressionStream : Stream
{
#if !LEGACY_DOTNET
    public override async ValueTask DisposeAsync()
#else
    public async ValueTask DisposeAsync()
#endif
    {
        if (compressor == null)
        {
#if LEGACY_DOTNET
            Dispose(true);
            GC.SuppressFinalize(this);
            await Task.CompletedTask.ConfigureAwait(false);
#else
            await base.DisposeAsync().ConfigureAwait(false);
#endif
            return;
        }

        try
        {
            await FlushInternalAsync(ZSTD_EndDirective.ZSTD_e_end).ConfigureAwait(false);
        }
        finally
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }
#if LEGACY_DOTNET
        Dispose(true);
        await Task.CompletedTask.ConfigureAwait(false);
#else
        await base.DisposeAsync().ConfigureAwait(false);
#endif
    }

    public override async Task FlushAsync(CancellationToken cancellationToken) =>
        await FlushInternalAsync(ZSTD_EndDirective.ZSTD_e_flush, cancellationToken)
            .ConfigureAwait(false);

    private async ValueTask FlushInternalAsync(
        ZSTD_EndDirective directive,
        CancellationToken cancellationToken = default
    ) => await WriteInternalAsync(null, directive, cancellationToken).ConfigureAwait(false);

#if !LEGACY_DOTNET
    private async ValueTask WriteInternalAsync(
        ReadOnlyMemory<byte>? buffer,
        ZSTD_EndDirective directive,
        CancellationToken cancellationToken = default
    )
#else
    private async ValueTask WriteInternalAsync(
        ReadOnlyMemory<byte>? buffer,
        ZSTD_EndDirective directive,
        CancellationToken cancellationToken = default
    )
#endif
    {
        EnsureNotDisposed();

        var input = new ZSTD_inBuffer_s
        {
            pos = 0,
            size = buffer.HasValue ? (nuint)buffer.Value.Length : 0,
        };
        nuint remaining;
        do
        {
            output.pos = 0;
            remaining = CompressStream(
                ref input,
                buffer.HasValue ? buffer.Value.Span : null,
                directive
            );

            var written = (int)output.pos;
            if (written > 0)
            {
                await innerStream
                    .WriteAsync(outputBuffer, 0, written, cancellationToken)
                    .ConfigureAwait(false);
            }
        } while (
            directive == ZSTD_EndDirective.ZSTD_e_continue ? input.pos < input.size : remaining > 0
        );
    }

#if !LEGACY_DOTNET

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) =>
        await WriteInternalAsync(buffer, ZSTD_EndDirective.ZSTD_e_continue, cancellationToken)
            .ConfigureAwait(false);
#else

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) =>
        await WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken)
            .ConfigureAwait(false);

    public async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) =>
        await WriteInternalAsync(buffer, ZSTD_EndDirective.ZSTD_e_continue, cancellationToken)
            .ConfigureAwait(false);
#endif
}
