using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA.RangeCoder;
using SharpCompress.Compressors.PPMd.H;
using SharpCompress.Compressors.PPMd.I1;

namespace SharpCompress.Compressors.PPMd;

public class PpmdStream : Stream
{
    private readonly PpmdProperties _properties;
    private readonly Stream _stream;
    private readonly bool _compress;
    private Model? _model;
    private ModelPpm? _modelH;
    private Decoder? _decoder;
    private long _position;
    private bool _isDisposed;

    private PpmdStream(PpmdProperties properties, Stream stream, bool compress)
    {
        _properties = properties;
        _stream = stream;
        _compress = compress;

        InitializeSync(stream, compress);
    }

    private PpmdStream(
        PpmdProperties properties,
        Stream stream,
        bool compress,
        bool skipInitialization
    )
    {
        _properties = properties;
        _stream = stream;
        _compress = compress;

        // Skip initialization - used by CreateAsync
    }

    private void InitializeSync(Stream stream, bool compress)
    {
        if (_properties.Version == PpmdVersion.I1)
        {
            _model = new Model();
            if (compress)
            {
                _model.EncodeStart(_properties);
            }
            else
            {
                _model.DecodeStart(stream, _properties);
            }
        }
        if (_properties.Version == PpmdVersion.H)
        {
            _modelH = new ModelPpm();
            if (compress)
            {
                throw new NotImplementedException();
            }
            _modelH.DecodeInit(stream, _properties.ModelOrder, _properties.AllocatorSize);
        }
        if (_properties.Version == PpmdVersion.H7Z)
        {
            _modelH = new ModelPpm();
            if (compress)
            {
                throw new NotImplementedException();
            }
            _modelH.DecodeInit(null, _properties.ModelOrder, _properties.AllocatorSize);
            _decoder = new Decoder();
            _decoder.Init(stream);
        }
    }

    public static PpmdStream Create(PpmdProperties properties, Stream stream, bool compress) =>
        new PpmdStream(properties, stream, compress);

    public static async ValueTask<PpmdStream> CreateAsync(
        PpmdProperties properties,
        Stream stream,
        bool compress,
        CancellationToken cancellationToken = default
    )
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (properties.Version == PpmdVersion.H && compress)
        {
            throw new NotImplementedException("PPMd H version compression not supported");
        }

        if (properties.Version == PpmdVersion.H7Z && compress)
        {
            throw new NotImplementedException("PPMd H7Z version compression not supported");
        }

        var instance = new PpmdStream(properties, stream, compress, skipInitialization: true);

        try
        {
            if (properties.Version == PpmdVersion.I1)
            {
                instance._model = new Model();
                if (compress)
                {
                    instance._model.EncodeStart(properties);
                }
                else
                {
                    await instance
                        ._model.DecodeStartAsync(stream, properties, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            else if (properties.Version == PpmdVersion.H)
            {
                instance._modelH = new ModelPpm();
                await instance
                    ._modelH.DecodeInitAsync(
                        stream,
                        properties.ModelOrder,
                        properties.AllocatorSize,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            else if (properties.Version == PpmdVersion.H7Z)
            {
                instance._modelH = new ModelPpm();
                await instance
                    ._modelH.DecodeInitAsync(
                        null,
                        properties.ModelOrder,
                        properties.AllocatorSize,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                instance._decoder = new Decoder();
                await instance._decoder.InitAsync(stream, cancellationToken).ConfigureAwait(false);
            }

            return instance;
        }
        catch
        {
            instance.Dispose();
            throw;
        }
    }

    public override bool CanRead => !_compress;

    public override bool CanSeek => false;

    public override bool CanWrite => _compress;

    public override void Flush() { }

    protected override void Dispose(bool isDisposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;
        if (isDisposing)
        {
            if (_compress)
            {
                _model.NotNull().EncodeBlock(_stream, new MemoryStream(), true);
            }
        }
        base.Dispose(isDisposing);
    }

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_compress)
        {
            return 0;
        }
        var size = 0;
        if (_properties.Version == PpmdVersion.I1)
        {
            size = _model.NotNull().DecodeBlock(_stream, buffer, offset, count);
        }
        if (_properties.Version == PpmdVersion.H)
        {
            int c;
            while (size < count && (c = _modelH.NotNull().DecodeChar()) >= 0)
            {
                buffer[offset++] = (byte)c;
                size++;
            }
        }
        if (_properties.Version == PpmdVersion.H7Z)
        {
            int c;
            while (size < count && (c = _modelH.NotNull().DecodeChar(_decoder.NotNull())) >= 0)
            {
                buffer[offset++] = (byte)c;
                size++;
            }
        }
        _position += size;
        return size;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken
    )
    {
        if (_compress)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var size = 0;
        if (_properties.Version == PpmdVersion.I1)
        {
            size = await _model.NotNull()
                .DecodeBlockAsync(_stream, buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
        }
        if (_properties.Version == PpmdVersion.H)
        {
            int c;
            while (
                size < count
                && (c = await _modelH.NotNull().DecodeCharAsync(cancellationToken).ConfigureAwait(false)) >= 0
            )
            {
                buffer[offset++] = (byte)c;
                size++;
            }
        }
        if (_properties.Version == PpmdVersion.H7Z)
        {
            int c;
            while (
                size < count
                && (
                    c = await _modelH.NotNull()
                        .DecodeCharAsync(_decoder.NotNull(), cancellationToken)
                        .ConfigureAwait(false)
                ) >= 0
            )
            {
                buffer[offset++] = (byte)c;
                size++;
            }
        }
        _position += size;
        return size;
    }

#if !LEGACY_DOTNET
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        if (_compress)
        {
            return 0;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var size = 0;
        var offset = 0;
        var count = buffer.Length;

        if (_properties.Version == PpmdVersion.I1)
        {
            // Need to use a temporary buffer since DecodeBlockAsync works with byte[]
            var tempBuffer = new byte[count];
            size = await _model.NotNull()
                .DecodeBlockAsync(_stream, tempBuffer, 0, count, cancellationToken)
                .ConfigureAwait(false);
            tempBuffer.AsMemory(0, size).CopyTo(buffer);
        }
        if (_properties.Version == PpmdVersion.H)
        {
            int c;
            while (
                size < count
                && (c = await _modelH.NotNull().DecodeCharAsync(cancellationToken).ConfigureAwait(false)) >= 0
            )
            {
                buffer.Span[offset++] = (byte)c;
                size++;
            }
        }
        if (_properties.Version == PpmdVersion.H7Z)
        {
            int c;
            while (
                size < count
                && (
                    c = await _modelH.NotNull()
                        .DecodeCharAsync(_decoder.NotNull(), cancellationToken)
                        .ConfigureAwait(false)
                ) >= 0
            )
            {
                buffer.Span[offset++] = (byte)c;
                size++;
            }
        }
        _position += size;
        return size;
    }
#endif

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_compress)
        {
            _model.NotNull().EncodeBlock(_stream, new MemoryStream(buffer, offset, count), false);
        }
    }
}
