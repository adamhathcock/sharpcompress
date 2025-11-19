using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public class XZFooter
{
    private readonly BinaryReader _reader;
    private static ReadOnlySpan<byte> _magicBytes => "YZ"u8;
    public long StreamStartPosition { get; private set; }
    public long BackwardSize { get; private set; }
    public byte[]? StreamFlags { get; private set; }

    public XZFooter(BinaryReader reader)
    {
        _reader = reader;
        StreamStartPosition = reader.BaseStream.Position;
    }

    public static XZFooter FromStream(Stream stream)
    {
        var footer = new XZFooter(new BinaryReader(stream, Encoding.UTF8, true));
        footer.Process();
        return footer;
    }

    public static async Task<XZFooter> FromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var footer = new XZFooter(new BinaryReader(stream, Encoding.UTF8, true));
        await footer.ProcessAsync(cancellationToken).ConfigureAwait(false);
        return footer;
    }

    public void Process()
    {
        var crc = _reader.ReadLittleEndianUInt32();
        var footerBytes = _reader.ReadBytes(6);
        var myCrc = Crc32.Compute(footerBytes);
        if (crc != myCrc)
        {
            throw new InvalidFormatException("Footer corrupt");
        }

        using (var stream = new MemoryStream(footerBytes))
        using (var reader = new BinaryReader(stream))
        {
            BackwardSize = (reader.ReadLittleEndianUInt32() + 1) * 4;
            StreamFlags = reader.ReadBytes(2);
        }
        var magBy = _reader.ReadBytes(2);
        if (!magBy.AsSpan().SequenceEqual(_magicBytes))
        {
            throw new InvalidFormatException("Magic footer missing");
        }
    }

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        var crc = await _reader
            .BaseStream.ReadLittleEndianUInt32Async(cancellationToken)
            .ConfigureAwait(false);
        var footerBytes = await _reader.ReadBytesAsync(6, cancellationToken).ConfigureAwait(false);
        var myCrc = Crc32.Compute(footerBytes);
        if (crc != myCrc)
        {
            throw new InvalidFormatException("Footer corrupt");
        }

        using (var stream = new MemoryStream(footerBytes))
        using (var reader = new BinaryReader(stream))
        {
            BackwardSize = (reader.ReadLittleEndianUInt32() + 1) * 4;
            StreamFlags = reader.ReadBytes(2);
        }
        var magBy = await _reader.ReadBytesAsync(2, cancellationToken).ConfigureAwait(false);
        if (!magBy.AsSpan().SequenceEqual(_magicBytes))
        {
            throw new InvalidFormatException("Magic footer missing");
        }
    }
}
