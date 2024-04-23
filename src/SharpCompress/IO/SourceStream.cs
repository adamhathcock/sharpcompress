using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Readers;

namespace SharpCompress.IO;

public class SourceStream : Stream
{
    private long _prevSize;
    private readonly List<FileInfo> _files;
    private readonly List<Stream> _streams;
    private readonly Func<int, FileInfo?>? _getFilePart;
    private readonly Func<int, Stream?>? _getStreamPart;
    private int _stream;

    public SourceStream(FileInfo file, Func<int, FileInfo?> getPart, ReaderOptions options)
        : this(null, null, file, getPart, options) { }

    public SourceStream(Stream stream, Func<int, Stream?> getPart, ReaderOptions options)
        : this(stream, getPart, null, null, options) { }

    private SourceStream(
        Stream? stream,
        Func<int, Stream?>? getStreamPart,
        FileInfo? file,
        Func<int, FileInfo?>? getFilePart,
        ReaderOptions options
    )
    {
        ReaderOptions = options;
        _files = new List<FileInfo>();
        _streams = new List<Stream>();
        IsFileMode = file != null;
        IsVolumes = false;

        if (!IsFileMode)
        {
            _streams.Add(stream!);
            _getStreamPart = getStreamPart;
            _getFilePart = _ => null;
            if (stream is FileStream fileStream)
            {
                _files.Add(new FileInfo(fileStream.Name));
            }
        }
        else
        {
            _files.Add(file!);
            _streams.Add(_files[0].OpenRead());
            _getFilePart = getFilePart;
            _getStreamPart = _ => null;
        }
        _stream = 0;
        _prevSize = 0;
    }

    public void LoadAllParts()
    {
        for (var i = 1; SetStream(i); i++) { }
        SetStream(0);
    }

    public bool IsVolumes { get; set; }

    public ReaderOptions ReaderOptions { get; }
    public bool IsFileMode { get; }

    public IEnumerable<FileInfo> Files => _files;
    public IEnumerable<Stream> Streams => _streams;

    private Stream Current => _streams[_stream];

    public bool LoadStream(int index) //ensure all parts to id are loaded
    {
        while (_streams.Count <= index)
        {
            if (IsFileMode)
            {
                var f = _getFilePart.NotNull("GetFilePart is null")(_streams.Count);
                if (f == null)
                {
                    _stream = _streams.Count - 1;
                    return false;
                }
                //throw new Exception($"File part {idx} not available.");
                _files.Add(f);
                _streams.Add(_files.Last().OpenRead());
            }
            else
            {
                var s = _getStreamPart.NotNull("GetStreamPart is null")(_streams.Count);
                if (s == null)
                {
                    _stream = _streams.Count - 1;
                    return false;
                }
                //throw new Exception($"Stream part {idx} not available.");
                _streams.Add(s);
                if (s is FileStream stream)
                {
                    _files.Add(new FileInfo(stream.Name));
                }
            }
        }
        return true;
    }

    public bool SetStream(int idx) //allow caller to switch part in multipart
    {
        if (LoadStream(idx))
        {
            _stream = idx;
        }

        return _stream == idx;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => !IsVolumes ? _streams.Sum(a => a.Length) : Current.Length;

    public override long Position
    {
        get => _prevSize + Current.Position; //_prevSize is 0 for multi-volume
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush() => Current.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        var total = count;
        var r = -1;

        while (count != 0 && r != 0)
        {
            r = Current.Read(
                buffer,
                offset,
                (int)Math.Min(count, Current.Length - Current.Position)
            );
            count -= r;
            offset += r;

            if (!IsVolumes && count != 0 && Current.Position == Current.Length)
            {
                var length = Current.Length;

                // Load next file if present
                if (!SetStream(_stream + 1))
                {
                    break;
                }

                // Current stream switched
                // Add length of previous stream
                _prevSize += length;
                Current.Seek(0, SeekOrigin.Begin);
                r = -1; //BugFix: reset to allow loop if count is still not 0 - was breaking split zipx (lzma xz etc)
            }
        }

        return total - count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var pos = Position;
        switch (origin)
        {
            case SeekOrigin.Begin:
                pos = offset;
                break;
            case SeekOrigin.Current:
                pos += offset;
                break;
            case SeekOrigin.End:
                pos = Length + offset;
                break;
        }

        _prevSize = 0;
        if (!IsVolumes)
        {
            SetStream(0);
            while (_prevSize + Current.Length < pos)
            {
                _prevSize += Current.Length;
                SetStream(_stream + 1);
            }
        }

        if (pos != _prevSize + Current.Position)
        {
            Current.Seek(pos - _prevSize, SeekOrigin.Begin);
        }

        return pos;
    }

    public override void SetLength(long value) => throw new NotImplementedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotImplementedException();

    public override void Close()
    {
        if (IsFileMode || !ReaderOptions.LeaveStreamOpen) //close if file mode or options specify it
        {
            foreach (var stream in _streams)
            {
                try
                {
                    stream.Dispose();
                }
                catch
                {
                    // ignored
                }
            }
            _streams.Clear();
            _files.Clear();
        }
    }

    protected override void Dispose(bool disposing)
    {
        Close();
        base.Dispose(disposing);
    }
}
