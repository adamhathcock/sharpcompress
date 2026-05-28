using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public partial class XZFooter
{
    public static async ValueTask<XZFooter> FromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var footer = new XZFooter(new BinaryReader(stream, Encoding.UTF8, true));
        await footer.ProcessAsync(cancellationToken).ConfigureAwait(false);
        return footer;
    }

    public async ValueTask ProcessAsync(CancellationToken cancellationToken = default)
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
