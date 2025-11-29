using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Readers;

namespace SharpCompress.IO;

/// <summary>
/// A stream that unifies multiple byte sources (files or streams) into a single readable stream.
///
/// <para>
/// SourceStream handles two distinct modes controlled by <see cref="IsVolumes"/>:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Split Mode (IsVolumes=false)</b>: Multiple files/streams are treated as one
/// contiguous byte sequence. Reading seamlessly transitions from one source to the
/// next. Position and Length span all sources combined. This is used for split
/// archives where file data is simply split across multiple physical files.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Volume Mode (IsVolumes=true)</b>: Each file/stream is treated as an independent
/// unit. Position and Length refer only to the current source. This is used for
/// multi-volume archives where each volume has its own headers and structure.
/// </description>
/// </item>
/// </list>
///
/// <para>
/// This abstraction works with <see cref="IByteSource"/> to provide raw byte access
/// and with <see cref="Common.Volume"/> to add archive-specific semantics.
/// </para>
///
/// <para>
/// Format-specific behaviors:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>7Zip</b>: Uses split mode. The SourceStream presents the entire archive
/// as one stream, and 7Zip handles internal "folders" (compression units)
/// that may contain contiguous compressed data for multiple files.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>RAR</b>: Can use either mode. Multi-volume RAR uses volume mode where
/// each .rar/.r00/.r01 file is a separate volume. SOLID RAR archives have
/// internal contiguous byte streams for decompression.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>ZIP</b>: Can use either mode. Split ZIP (.z01, .z02, .zip) uses split mode.
/// Multi-volume ZIP uses volume mode.
/// </description>
/// </item>
/// </list>
/// </summary>
public class SourceStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }

    Stream IStreamStack.BaseStream() => _streams[_stream];

    int IStreamStack.BufferSize
    {
        get => 0;
        set { return; }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { return; }
    }

    void IStreamStack.SetPosition(long position) { }

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

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(SourceStream));
#endif
    }

    /// <summary>
    /// Loads all available parts/volumes by calling the getPart function
    /// until it returns null. Resets to the first stream after loading.
    /// </summary>
    public void LoadAllParts()
    {
        for (var i = 1; SetStream(i); i++) { }
        SetStream(0);
    }

    /// <summary>
    /// Gets or sets whether this SourceStream operates in volume mode.
    /// When true, each stream is treated as an independent volume with its own
    /// position and length. When false (default), all streams are treated as
    /// one contiguous byte sequence.
    /// </summary>
    public bool IsVolumes { get; set; }

    /// <summary>
    /// Gets the reader options associated with this source stream.
    /// </summary>
    public ReaderOptions ReaderOptions { get; }

    /// <summary>
    /// Gets whether this SourceStream was created from a FileInfo (true)
    /// or from a Stream (false).
    /// </summary>
    public bool IsFileMode { get; }

    /// <summary>
    /// Gets the collection of FileInfo objects for each loaded source.
    /// May be empty if sources are streams without file associations.
    /// </summary>
    public IEnumerable<FileInfo> Files => _files;

    /// <summary>
    /// Gets the collection of underlying streams for each loaded source.
    /// </summary>
    public IEnumerable<Stream> Streams => _streams;

    private Stream Current => _streams[_stream];

    /// <summary>
    /// Ensures that streams up to and including the specified index are loaded.
    /// </summary>
    /// <param name="index">The stream index to load.</param>
    /// <returns>True if the stream at the index was successfully loaded; false otherwise.</returns>
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

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (count <= 0)
        {
            return 0;
        }

        var total = count;
        var r = -1;

        while (count != 0 && r != 0)
        {
            r = await Current
                .ReadAsync(
                    buffer,
                    offset,
                    (int)Math.Min(count, Current.Length - Current.Position),
                    cancellationToken
                )
                .ConfigureAwait(false);
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

#if !NETFRAMEWORK && !NETSTANDARD2_0

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (buffer.Length <= 0)
        {
            return 0;
        }

        var total = buffer.Length;
        var count = buffer.Length;
        var offset = 0;
        var r = -1;

        while (count != 0 && r != 0)
        {
            r = await Current
                .ReadAsync(
                    buffer.Slice(offset, (int)Math.Min(count, Current.Length - Current.Position)),
                    cancellationToken
                )
                .ConfigureAwait(false);
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
                r = -1;
            }
        }

        return total - count;
    }
#endif

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
#if DEBUG_STREAMS
        this.DebugDispose(typeof(SourceStream));
#endif
        Close();
        base.Dispose(disposing);
    }
}
