using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SharpCompress.Compressors.RLE90;

namespace SharpCompress.Compressors.Squeezed;

[CLSCompliant(true)]
public partial class SqueezeStream : Stream
{
    private readonly Stream _stream;
    private const int NUMVALS = 257;
    private const int SPEOF = 256;

    private Stream _decodedStream = null!;

    private SqueezeStream(Stream stream, int compressedSize)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    public static SqueezeStream Create(Stream stream, int compressedSize)
    {
        var squeezeStream = new SqueezeStream(stream, compressedSize);
        squeezeStream._decodedStream = squeezeStream.BuildDecodedStream();

        return squeezeStream;
    }

    protected override void Dispose(bool disposing)
    {
        _decodedStream?.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count)
    {
        return _decodedStream.Read(buffer, offset, count);
    }

    private Stream BuildDecodedStream()
    {
        using var binaryReader = new BinaryReader(_stream, Encoding.Default, leaveOpen: true);
        int numnodes = binaryReader.ReadUInt16();

        if (numnodes >= NUMVALS || numnodes == 0)
        {
            return new MemoryStream(Array.Empty<byte>());
        }

        var dnode = new int[numnodes, 2];
        for (int j = 0; j < numnodes; j++)
        {
            dnode[j, 0] = binaryReader.ReadInt16();
            dnode[j, 1] = binaryReader.ReadInt16();
        }

        var bitReader = new BitReader(_stream);
        var huffmanDecoded = new MemoryStream();
        int i = 0;

        while (true)
        {
            i = dnode[i, bitReader.ReadBit() ? 1 : 0];
            if (i < 0)
            {
                i = -(i + 1);
                if (i == SPEOF)
                {
                    break;
                }
                huffmanDecoded.WriteByte((byte)i);
                i = 0;
            }
        }

        huffmanDecoded.Position = 0;
        return new RunLength90Stream(huffmanDecoded, (int)huffmanDecoded.Length);
    }
}
