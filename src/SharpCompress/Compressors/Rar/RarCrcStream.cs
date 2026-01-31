using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Rar;

internal partial class RarCrcStream : RarStream, IStreamStack
{
    Stream IStreamStack.BaseStream() => readStream;

    private readonly MultiVolumeReadOnlyStreamBase readStream;
    private uint currentCrc;
    private readonly bool disableCRC;

    private RarCrcStream(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStreamBase readStream
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

    // Async methods moved to RarCrcStream.Async.cs

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
            && GetCrc() != BitConverter.ToUInt32(readStream.NotNull().CurrentCrc.NotNull(), 0)
            && count != 0
        )
        {
            // NOTE: we use the last FileHeader in a multipart volume to check CRC
            throw new InvalidFormatException("file crc mismatch");
        }

        return result;
    }
}
