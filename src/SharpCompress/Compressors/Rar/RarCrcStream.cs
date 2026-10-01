using System;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;

namespace SharpCompress.Compressors.Rar;

internal partial class RarCrcStream : RarStream
{
    private readonly string? _key;
    private readonly MultiVolumeReadOnlyStreamBase _readStream;
    private uint _currentCrc;
    private readonly bool _disableCrc;

    private RarCrcStream(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStreamBase readStream
    )
        : base(unpack, fileHeader, readStream)
    {
        this._readStream = readStream;
        _key = fileHeader.FileName;
        _disableCrc = fileHeader.IsEncrypted;
        ResetCrc();
    }

    public static RarCrcStream Create(
        IRarUnpack unpack,
        FileHeader fileHeader,
        MultiVolumeReadOnlyStream readStream
    )
    {
        var stream = new RarCrcStream(unpack, fileHeader, readStream);
        return stream;
    }

    // Async methods moved to RarCrcStream.Async.cs
    public uint GetCrc() => ~_currentCrc;

    public void ResetCrc() => _currentCrc = 0xffffffff;

    public override int Read(byte[] buffer, int offset, int count)
    {
        var result = base.Read(buffer, offset, count);
        if (result != 0)
        {
            _currentCrc = RarCRC.CheckCrc(_currentCrc, buffer, offset, result);
        }
        else if (
            !_disableCrc
            && GetCrc() != BitConverter.ToUInt32(_readStream.NotNull().CurrentCrc.NotNull(), 0)
            && count != 0
        )
        {
            // NOTE: we use the last FileHeader in a multipart volume to check CRC
            throw new InvalidFormatException("file crc mismatch: " + _key);
        }

        return result;
    }
}
