using System;
using System.IO;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common;

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
