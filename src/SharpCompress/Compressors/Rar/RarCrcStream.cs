using System;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar;

internal class RarCrcStream : RarStream
{
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
        disableCRC = fileHeader.IsEncrypted;
        ResetCrc();
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
