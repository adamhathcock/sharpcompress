using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common;

public abstract partial class Volume : IVolume, IAsyncDisposable
{
    private readonly Stream _baseStream;
    private readonly Stream _actualStream;

    internal Volume(Stream stream, ReaderOptions? readerOptions, int index = 0)
    {
        Index = index;
        ReaderOptions = readerOptions ?? new ReaderOptions();
        _baseStream = stream;

        if (stream is RewindableStream ss)
        {
            ss.Rewind();
        }
        if (ReaderOptions.LeaveStreamOpen)
        {
            stream = new NonDisposingStream(stream);
        }

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
