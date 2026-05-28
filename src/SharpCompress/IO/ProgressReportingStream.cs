using System;
using System.IO;
using SharpCompress.Common;

namespace SharpCompress.IO;

/// <summary>
/// A stream wrapper that reports progress as data is read from the source.
/// Used to track compression or extraction progress by wrapping the source stream.
/// </summary>
internal sealed partial class ProgressReportingStream : Stream
{
    private readonly Stream _baseStream;
    private readonly IProgress<ProgressReport> _progress;
    private readonly string _entryPath;
    private readonly long? _totalBytes;
    private long _bytesTransferred;
    private readonly bool _leaveOpen;

    public ProgressReportingStream(
        Stream baseStream,
        IProgress<ProgressReport> progress,
        string entryPath,
        long? totalBytes,
        bool leaveOpen = false
    )
    {
        _baseStream = baseStream;
        _progress = progress;
        _entryPath = entryPath;
        _totalBytes = totalBytes;
        _leaveOpen = leaveOpen;
    }

    public override bool CanRead => _baseStream.CanRead;

    public override bool CanSeek => _baseStream.CanSeek;

    public override bool CanWrite => false;

    public override long Length => _baseStream.Length;

    public override long Position
    {
        get => _baseStream.Position;
        set =>
            throw new NotSupportedException(
                "Directly setting Position is not supported in ProgressReportingStream to maintain progress tracking integrity."
            );
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _baseStream.Read(buffer, offset, count);
        if (bytesRead > 0)
        {
            _bytesTransferred += bytesRead;
            ReportProgress();
        }
        return bytesRead;
    }

#if !LEGACY_DOTNET
    public override int Read(Span<byte> buffer)
    {
        var bytesRead = _baseStream.Read(buffer);
        if (bytesRead > 0)
        {
            _bytesTransferred += bytesRead;
            ReportProgress();
        }
        return bytesRead;
    }
#endif

    public override int ReadByte()
    {
        var value = _baseStream.ReadByte();
        if (value != -1)
        {
            _bytesTransferred++;
            ReportProgress();
        }
        return value;
    }

    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

    public override void SetLength(long value) => _baseStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException(
            "ProgressReportingStream is designed for read operations to track progress."
        );

    private void ReportProgress()
    {
        _progress.Report(new ProgressReport(_entryPath, _bytesTransferred, _totalBytes));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _baseStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
