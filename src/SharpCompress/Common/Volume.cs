using System;
using System.IO;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common;

/// <summary>
/// Base class for archive volumes. A volume represents a single physical
/// archive file or stream that may contain entries or parts of entries.
///
/// <para>
/// The relationship between <see cref="Volume"/>, <see cref="SourceStream"/>,
/// and <see cref="IByteSource"/> is:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>IByteSource</b> is the lowest level - it provides access to raw bytes
/// from a file or stream, with no archive-specific logic.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>SourceStream</b> combines multiple byte sources into a unified stream,
/// handling the distinction between split archives (contiguous bytes) and
/// multi-volume archives (independent units).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Volume</b> wraps a stream with archive-specific metadata and behavior.
/// Format-specific subclasses (ZipVolume, RarVolume, etc.) add format-specific
/// properties and methods.
/// </description>
/// </item>
/// </list>
/// </summary>
public abstract class Volume : IVolume
{
    private readonly Stream _baseStream;
    private readonly Stream _actualStream;

    internal Volume(Stream stream, ReaderOptions? readerOptions, int index = 0)
    {
        Index = index;
        ReaderOptions = readerOptions ?? new ReaderOptions();
        _baseStream = stream;
        if (ReaderOptions.LeaveStreamOpen)
        {
            stream = SharpCompressStream.Create(stream, leaveOpen: true);
        }

        if (stream is IStreamStack ss)
            ss.SetBuffer(ReaderOptions.BufferSize, true);

        _actualStream = stream;
    }

    internal Stream Stream => _actualStream;

    protected ReaderOptions ReaderOptions { get; }

    /// <summary>
    /// RarArchive is the first volume of a multi-part archive.
    /// Only Rar 3.0 format and higher
    /// </summary>
    public virtual bool IsFirstVolume => true;

    public virtual int Index { get; internal set; }

    public string? FileName => (_baseStream as FileStream)?.Name;

    /// <summary>
    /// RarArchive is part of a multi-part archive.
    /// </summary>
    public virtual bool IsMultiVolume => true;

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _actualStream.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
