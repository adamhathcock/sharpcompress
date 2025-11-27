using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.IO;

/// <summary>
/// A stream wrapper that reports progress as data is written.
/// </summary>
internal sealed class ProgressReportingStream : Stream
{
    private readonly Stream _baseStream;
    private readonly IProgress<CompressionProgress> _progress;
    private readonly string _entryPath;
    private readonly long? _totalBytes;
    private long _bytesWritten;

    public ProgressReportingStream(
        Stream baseStream,
        IProgress<CompressionProgress> progress,
        string entryPath,
        long? totalBytes
    )
    {
        _baseStream = baseStream;
        _progress = progress;
        _entryPath = entryPath;
        _totalBytes = totalBytes;
    }

    public override bool CanRead => _baseStream.CanRead;

    public override bool CanSeek => _baseStream.CanSeek;

    public override bool CanWrite => _baseStream.CanWrite;

    public override long Length => _baseStream.Length;

    public override long Position
    {
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        _baseStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) =>
        _baseStream.Seek(offset, origin);

    public override void SetLength(long value) => _baseStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
    {
        _baseStream.Write(buffer, offset, count);
        _bytesWritten += count;
        ReportProgress();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _baseStream.Write(buffer);
        _bytesWritten += buffer.Length;
        ReportProgress();
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        await _baseStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        _bytesWritten += count;
        ReportProgress();
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        await _baseStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        _bytesWritten += buffer.Length;
        ReportProgress();
    }

    public override void WriteByte(byte value)
    {
        _baseStream.WriteByte(value);
        _bytesWritten++;
        ReportProgress();
    }

    private void ReportProgress()
    {
        _progress.Report(new CompressionProgress(_entryPath, _bytesWritten, _totalBytes));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _baseStream.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
