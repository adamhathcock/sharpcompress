using System;
using System.IO;
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

    public RarCrcStream(
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
}
