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
/// Internally, SourceStream uses <see cref="IByteSource"/> to manage its underlying
/// sources. This allows consistent handling of both file-based and stream-based sources,
/// while the <see cref="Common.Volume"/> abstraction adds archive-specific semantics.
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

    Stream IStreamStack.BaseStream() => _openStreams[_currentSourceIndex];

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
    private readonly List<IByteSource> _sources;
    private readonly List<Stream> _openStreams;
    private readonly Func<int, IByteSource?>? _getNextSource;
    private int _currentSourceIndex;

    /// <summary>
    /// Creates a SourceStream from a file, with a function to get additional file parts.
    /// </summary>
    /// <param name="file">The initial file to read from.</param>
    /// <param name="getPart">Function that returns additional file parts by index, or null when no more parts.</param>
    /// <param name="options">Reader options.</param>
    public SourceStream(FileInfo file, Func<int, FileInfo?> getPart, ReaderOptions options)
        : this(
            new FileByteSource(file, 0),
            index =>
            {
                var f = getPart(index);
                return f != null ? new FileByteSource(f, index) : null;
            },
            options
        ) { }

    /// <summary>
    /// Creates a SourceStream from a stream, with a function to get additional stream parts.
    /// </summary>
    /// <param name="stream">The initial stream to read from.</param>
    /// <param name="getPart">Function that returns additional stream parts by index, or null when no more parts.</param>
    /// <param name="options">Reader options.</param>
    public SourceStream(Stream stream, Func<int, Stream?> getPart, ReaderOptions options)
        : this(
            new StreamByteSource(stream, 0),
            index =>
            {
                var s = getPart(index);
                return s != null ? new StreamByteSource(s, index) : null;
            },
            options
        ) { }

    /// <summary>
    /// Creates a SourceStream from an initial byte source with a function to get additional sources.
    /// </summary>
    /// <param name="initialSource">The initial byte source.</param>
    /// <param name="getNextSource">Function that returns additional byte sources by index, or null when no more sources.</param>
    /// <param name="options">Reader options.</param>
    public SourceStream(
        IByteSource initialSource,
        Func<int, IByteSource?>? getNextSource,
        ReaderOptions options
    )
    {
        ReaderOptions = options;
        _sources = new List<IByteSource> { initialSource };
        _openStreams = new List<Stream> { initialSource.OpenRead() };
        _getNextSource = getNextSource;
        _currentSourceIndex = 0;
        _prevSize = 0;
        IsVolumes = false;
        IsFileMode = initialSource is FileByteSource;

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(SourceStream));
#endif
    }

    /// <summary>
    /// Loads all available parts/volumes by calling the getNextSource function
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
    /// Gets the collection of loaded byte sources.
    /// </summary>
    internal IEnumerable<IByteSource> Sources => _sources;

    /// <summary>
    /// Gets the collection of FileInfo objects for each loaded source.
    /// May be empty if sources are streams without file associations.
    /// </summary>
    public IEnumerable<FileInfo> Files =>
        _sources.Where(s => s.FileName != null).Select(s => new FileInfo(s.FileName!));

    /// <summary>
    /// Gets the collection of underlying streams for each loaded source.
    /// </summary>
    public IEnumerable<Stream> Streams => _openStreams;

    private Stream Current => _openStreams[_currentSourceIndex];

    /// <summary>
    /// Ensures that sources up to and including the specified index are loaded.
    /// </summary>
    /// <param name="index">The source index to load.</param>
    /// <returns>True if the source at the index was successfully loaded; false otherwise.</returns>
    public bool LoadStream(int index)
    {
        while (_sources.Count <= index)
        {
            var nextSource = _getNextSource?.Invoke(_sources.Count);
            if (nextSource is null)
            {
                _currentSourceIndex = _sources.Count - 1;
                return false;
            }
            _sources.Add(nextSource);
            _openStreams.Add(nextSource.OpenRead());
        }
        return true;
    }

    /// <summary>
    /// Switches to the specified source index.
    /// </summary>
    /// <param name="idx">The source index to switch to.</param>
    /// <returns>True if the switch was successful; false otherwise.</returns>
    public bool SetStream(int idx)
    {
        if (LoadStream(idx))
        {
            _currentSourceIndex = idx;
        }

        return _currentSourceIndex == idx;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => !IsVolumes ? _openStreams.Sum(a => a.Length) : Current.Length;

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

                // Load next source if present
                if (!SetStream(_currentSourceIndex + 1))
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
                SetStream(_currentSourceIndex + 1);
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

                // Load next source if present
                if (!SetStream(_currentSourceIndex + 1))
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

                // Load next source if present
                if (!SetStream(_currentSourceIndex + 1))
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
            foreach (var stream in _openStreams)
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
            _openStreams.Clear();
            _sources.Clear();
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
