using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.ZStandard.Unsafe;

namespace SharpCompress.Compressors.ZStandard;

public class DecompressionStream : Stream
{
    private readonly Stream innerStream;
    private readonly byte[] inputBuffer;
    private readonly int inputBufferSize;
    private readonly bool preserveDecompressor;
    private readonly bool leaveOpen;
    private readonly bool checkEndOfStream;
    private Decompressor? decompressor;
    private ZSTD_inBuffer_s input;
    private nuint lastDecompressResult = 0;
    private bool contextDrained = true;

    public DecompressionStream(
        Stream stream,
        int bufferSize = 0,
        bool checkEndOfStream = true,
        bool leaveOpen = true
    )
        : this(stream, new Decompressor(), bufferSize, checkEndOfStream, false, leaveOpen) { }

    public DecompressionStream(
        Stream stream,
        Decompressor decompressor,
        int bufferSize = 0,
        bool checkEndOfStream = true,
        bool preserveDecompressor = true,
        bool leaveOpen = true
    )
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        if (!stream.CanRead)
            throw new ArgumentException("Stream is not readable", nameof(stream));

        if (bufferSize < 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        innerStream = stream;
        this.decompressor = decompressor;
        this.preserveDecompressor = preserveDecompressor;
        this.leaveOpen = leaveOpen;
        this.checkEndOfStream = checkEndOfStream;

        inputBufferSize =
            bufferSize > 0
                ? bufferSize
                : (int)Unsafe.Methods.ZSTD_DStreamInSize().EnsureZstdSuccess();
        inputBuffer = ArrayPool<byte>.Shared.Rent(inputBufferSize);
        input = new ZSTD_inBuffer_s { pos = (nuint)inputBufferSize, size = (nuint)inputBufferSize };
    }

    public void SetParameter(ZSTD_dParameter parameter, int value)
    {
        EnsureNotDisposed();
        decompressor.NotNull().SetParameter(parameter, value);
    }

    public int GetParameter(ZSTD_dParameter parameter)
    {
        EnsureNotDisposed();
        return decompressor.NotNull().GetParameter(parameter);
    }

    public void LoadDictionary(byte[] dict)
    {
        EnsureNotDisposed();
        decompressor.NotNull().LoadDictionary(dict);
    }

    ~DecompressionStream() => Dispose(false);

    protected override void Dispose(bool disposing)
    {
        if (decompressor == null)
            return;

        if (!preserveDecompressor)
        {
            decompressor.Dispose();
        }
        decompressor = null;

        if (inputBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(inputBuffer);
        }

        if (!leaveOpen)
        {
            innerStream.Dispose();
        }
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(new Span<byte>(buffer, offset, count));

#if !NETSTANDARD2_0 && !NETFRAMEWORK
    public override int Read(Span<byte> buffer)
#else
    public int Read(Span<byte> buffer)
#endif
    {
        EnsureNotDisposed();

        // Guard against infinite loop (output.pos would never become non-zero)
        if (buffer.Length == 0)
        {
            return 0;
        }

        var output = new ZSTD_outBuffer_s { pos = 0, size = (nuint)buffer.Length };
        while (true)
        {
            // If there is still input available, or there might be data buffered in the decompressor context, flush that out
            while (input.pos < input.size || !contextDrained)
            {
                nuint oldInputPos = input.pos;
                nuint result = DecompressStream(ref output, buffer);
                if (output.pos > 0 || oldInputPos != input.pos)
                {
                    // Keep result from last decompress call that made some progress, so we known if we're at end of frame
                    lastDecompressResult = result;
                }
                // If decompression filled the output buffer, there might still be data buffered in the decompressor context
                contextDrained = output.pos < output.size;
                // If we have data to return, return it immediately, so we won't stall on Read
                if (output.pos > 0)
                {
                    return (int)output.pos;
                }
            }

            // Otherwise, read some more input
            int bytesRead;
            if ((bytesRead = innerStream.Read(inputBuffer, 0, inputBufferSize)) == 0)
            {
                if (checkEndOfStream && lastDecompressResult != 0)
                {
                    throw new EndOfStreamException("Premature end of stream");
                }

                return 0;
            }

            input.size = (nuint)bytesRead;
            input.pos = 0;
        }
    }

#if !NETSTANDARD2_0 && !NETFRAMEWORK
    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
#else

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    ) => ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken);

    public async Task<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
#endif
    {
        EnsureNotDisposed();

        // Guard against infinite loop (output.pos would never become non-zero)
        if (buffer.Length == 0)
        {
            return 0;
        }

        var output = new ZSTD_outBuffer_s { pos = 0, size = (nuint)buffer.Length };
        while (true)
        {
            // If there is still input available, or there might be data buffered in the decompressor context, flush that out
            while (input.pos < input.size || !contextDrained)
            {
                nuint oldInputPos = input.pos;
                nuint result = DecompressStream(ref output, buffer.Span);
                if (output.pos > 0 || oldInputPos != input.pos)
                {
                    // Keep result from last decompress call that made some progress, so we known if we're at end of frame
                    lastDecompressResult = result;
                }
                // If decompression filled the output buffer, there might still be data buffered in the decompressor context
                contextDrained = output.pos < output.size;
                // If we have data to return, return it immediately, so we won't stall on Read
                if (output.pos > 0)
                {
                    return (int)output.pos;
                }
            }

            // Otherwise, read some more input
            int bytesRead;
            if (
                (
                    bytesRead = await innerStream
                        .ReadAsync(inputBuffer, 0, inputBufferSize, cancellationToken)
                        .ConfigureAwait(false)
                ) == 0
            )
            {
                if (checkEndOfStream && lastDecompressResult != 0)
                {
                    throw new EndOfStreamException("Premature end of stream");
                }

                return 0;
            }

            input.size = (nuint)bytesRead;
            input.pos = 0;
        }
    }

    private unsafe nuint DecompressStream(ref ZSTD_outBuffer_s output, Span<byte> outputBuffer)
    {
        fixed (byte* inputBufferPtr = inputBuffer)
        fixed (byte* outputBufferPtr = outputBuffer)
        {
            input.src = inputBufferPtr;
            output.dst = outputBufferPtr;
            return decompressor.NotNull().DecompressStream(ref input, ref output);
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private void EnsureNotDisposed()
    {
        if (decompressor == null)
            throw new ObjectDisposedException(nameof(DecompressionStream));
    }

#if NETSTANDARD2_0 || NETFRAMEWORK
    public virtual Task DisposeAsync()
    {
        try
        {
            Dispose();
            return Task.CompletedTask;
        }
        catch (Exception exc)
        {
            return Task.FromException(exc);
        }
    }
#endif
}
