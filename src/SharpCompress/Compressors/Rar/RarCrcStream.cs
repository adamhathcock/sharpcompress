using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Rar;

internal class RarCrcStream : RarStream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif

    Stream IStreamStack.BaseStream() => readStream;

    int IStreamStack.BufferSize
    {
        get => 0;
        set { }
    }
    int IStreamStack.BufferPosition
    {
        get => 0;
        set { }
    }

    void IStreamStack.SetPosition(long position) { }

    private readonly MultiVolumeReadOnlyStream readStream;
    private uint currentCrc;
    private readonly bool disableCRC;

    private RarCrcStream(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStream readStream
    )
        : base(unpack, fileHeader, readStream)
    {
        this.readStream = readStream;
#if DEBUG_STREAMS
        this.DebugConstruct(typeof(RarCrcStream));
#endif
        disableCRC = fileHeader.IsEncrypted;
        ResetCrc();
    }

    public static RarCrcStream Create(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStream readStream
    )
    {
        var stream = new RarCrcStream(unpack, fileHeader, readStream);
        stream.Initialize();
        return stream;
    }

    public static async Task<RarCrcStream> CreateAsync(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStream readStream,
        CancellationToken cancellationToken = default
    )
    {
        var stream = new RarCrcStream(unpack, fileHeader, readStream);
        await stream.InitializeAsync(cancellationToken);
        return stream;
    }

    protected override void Dispose(bool disposing)
    {
#if DEBUG_STREAMS
        this.DebugDispose(typeof(RarCrcStream));
#endif
        base.Dispose(disposing);
    }

    public uint GetCrc() => ~currentCrc;

    public void ResetCrc() => currentCrc = 0xffffffff;

    public override int Read(byte[] buffer, int offset, int count)
    {
        var result = base.Read(buffer, offset, count);
        if (result != 0)
        {
            currentCrc = RarCRC.CheckCrc(currentCrc, buffer, offset, result);
        }
        else if (
            !disableCRC
            && GetCrc() != BitConverter.ToUInt32(readStream.CurrentCrc, 0)
            && count != 0
        )
        {
            // NOTE: we use the last FileHeader in a multipart volume to check CRC
            throw new InvalidFormatException("file crc mismatch");
        }

        return result;
    }

    public override async System.Threading.Tasks.Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        System.Threading.CancellationToken cancellationToken
    )
    {
        var result = await base.ReadAsync(buffer, offset, count, cancellationToken)
            .ConfigureAwait(false);
        if (result != 0)
        {
            currentCrc = RarCRC.CheckCrc(currentCrc, buffer, offset, result);
        }
        else if (
            !disableCRC
            && GetCrc() != BitConverter.ToUInt32(readStream.CurrentCrc, 0)
            && count != 0
        )
        {
            // NOTE: we use the last FileHeader in a multipart volume to check CRC
            throw new InvalidFormatException("file crc mismatch");
        }

        return result;
    }

#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
    public override async System.Threading.Tasks.ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        System.Threading.CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (result != 0)
        {
            currentCrc = RarCRC.CheckCrc(currentCrc, buffer.Span, 0, result);
        }
        else if (
            !disableCRC
            && GetCrc() != BitConverter.ToUInt32(readStream.CurrentCrc, 0)
            && buffer.Length != 0
        )
        {
            // NOTE: we use the last FileHeader in a multipart volume to check CRC
            throw new InvalidFormatException("file crc mismatch");
        }

        return result;
    }
#endif
}
