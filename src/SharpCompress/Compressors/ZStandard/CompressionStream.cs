using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

public class CompressionStream : Stream
{
    private readonly Stream innerStream;
    private readonly byte[] outputBuffer;
    private readonly bool preserveCompressor;
    private readonly bool leaveOpen;
    private Compressor? compressor;
    private ZSTD_outBuffer_s output;

    public CompressionStream(
        Stream stream,
        int level = Compressor.DefaultCompressionLevel,
        int bufferSize = 0,
        bool leaveOpen = true
    )
        : this(stream, new Compressor(level), bufferSize, false, leaveOpen) { }

    public CompressionStream(
        Stream stream,
        Compressor compressor,
        int bufferSize = 0,
        bool preserveCompressor = true,
        bool leaveOpen = true
    )
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        if (!stream.CanWrite)
            throw new ArgumentException("Stream is not writable", nameof(stream));

        if (bufferSize < 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        innerStream = stream;
        this.compressor = compressor;
        this.preserveCompressor = preserveCompressor;
        this.leaveOpen = leaveOpen;

        var outputBufferSize =
            bufferSize > 0
                ? bufferSize
                : (int)Unsafe.Methods.ZSTD_CStreamOutSize().EnsureZstdSuccess();
        outputBuffer = ArrayPool<byte>.Shared.Rent(outputBufferSize);
        output = new ZSTD_outBuffer_s { pos = 0, size = (nuint)outputBufferSize };
    }

    public void SetParameter(ZSTD_cParameter parameter, int value)
    {
        EnsureNotDisposed();
        compressor.NotNull().SetParameter(parameter, value);
    }

    public int GetParameter(ZSTD_cParameter parameter)
    {
        EnsureNotDisposed();
        return compressor.NotNull().GetParameter(parameter);
    }

    public void LoadDictionary(byte[] dict)
    {
        EnsureNotDisposed();
        compressor.NotNull().LoadDictionary(dict);
    }

    ~CompressionStream() => Dispose(false);

#if !NETSTANDARD2_0 && !NETFRAMEWORK
    public override async ValueTask DisposeAsync()
#else
    public async Task DisposeAsync()
#endif
    {
        if (compressor == null)
            return;

        try
        {
            await FlushInternalAsync(ZSTD_EndDirective.ZSTD_e_end).ConfigureAwait(false);
        }
        finally
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (compressor == null)
            return;

        try
        {
            if (disposing)
                FlushInternal(ZSTD_EndDirective.ZSTD_e_end);
        }
        finally
        {
            ReleaseUnmanagedResources();
        }
    }

    private void ReleaseUnmanagedResources()
    {
        if (!preserveCompressor)
        {
            compressor.NotNull().Dispose();
        }
        compressor = null;

        if (outputBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(outputBuffer);
        }

        if (!leaveOpen)
        {
            innerStream.Dispose();
        }
    }

    public override void Flush() => FlushInternal(ZSTD_EndDirective.ZSTD_e_flush);

    public override async Task FlushAsync(CancellationToken cancellationToken) =>
        await FlushInternalAsync(ZSTD_EndDirective.ZSTD_e_flush, cancellationToken)
            .ConfigureAwait(false);

    private void FlushInternal(ZSTD_EndDirective directive) => WriteInternal(null, directive);

    private async Task FlushInternalAsync(
        ZSTD_EndDirective directive,
        CancellationToken cancellationToken = default
    ) => await WriteInternalAsync(null, directive, cancellationToken).ConfigureAwait(false);

    public override void Write(byte[] buffer, int offset, int count) =>
        Write(new ReadOnlySpan<byte>(buffer, offset, count));

#if !NETSTANDARD2_0 && !NETFRAMEWORK
    public override void Write(ReadOnlySpan<byte> buffer) =>
        WriteInternal(buffer, ZSTD_EndDirective.ZSTD_e_continue);
#else
    public void Write(ReadOnlySpan<byte> buffer) =>
        WriteInternal(buffer, ZSTD_EndDirective.ZSTD_e_continue);
#endif

    private void WriteInternal(ReadOnlySpan<byte> buffer, ZSTD_EndDirective directive)
    {
        EnsureNotDisposed();

        var input = new ZSTD_inBuffer_s
        {
            pos = 0,
            size = buffer != null ? (nuint)buffer.Length : 0,
        };
        nuint remaining;
        do
        {
            output.pos = 0;
            remaining = CompressStream(ref input, buffer, directive);

            var written = (int)output.pos;
            if (written > 0)
                innerStream.Write(outputBuffer, 0, written);
        } while (
            directive == ZSTD_EndDirective.ZSTD_e_continue ? input.pos < input.size : remaining > 0
        );
    }

#if !NETSTANDARD2_0 && !NETFRAMEWORK
    private async ValueTask WriteInternalAsync(
        ReadOnlyMemory<byte>? buffer,
        ZSTD_EndDirective directive,
        CancellationToken cancellationToken = default
    )
#else
    private async Task WriteInternalAsync(
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
                await innerStream
                    .WriteAsync(outputBuffer, 0, written, cancellationToken)
                    .ConfigureAwait(false);
        } while (
            directive == ZSTD_EndDirective.ZSTD_e_continue ? input.pos < input.size : remaining > 0
        );
    }

#if !NETSTANDARD2_0 && !NETFRAMEWORK

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

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken);

    public async Task WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    ) =>
        await WriteInternalAsync(buffer, ZSTD_EndDirective.ZSTD_e_continue, cancellationToken)
            .ConfigureAwait(false);
#endif

    internal unsafe nuint CompressStream(
        ref ZSTD_inBuffer_s input,
        ReadOnlySpan<byte> inputBuffer,
        ZSTD_EndDirective directive
    )
    {
        fixed (byte* inputBufferPtr = inputBuffer)
        fixed (byte* outputBufferPtr = outputBuffer)
        {
            input.src = inputBufferPtr;
            output.dst = outputBufferPtr;
            return compressor
                .NotNull()
                .CompressStream(ref input, ref output, directive)
                .EnsureZstdSuccess();
        }
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private void EnsureNotDisposed()
    {
        if (compressor == null)
            throw new ObjectDisposedException(nameof(CompressionStream));
    }

    public void SetPledgedSrcSize(ulong pledgedSrcSize)
    {
        EnsureNotDisposed();
        compressor.NotNull().SetPledgedSrcSize(pledgedSrcSize);
    }
}
