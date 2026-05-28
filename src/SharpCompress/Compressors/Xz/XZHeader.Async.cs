using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public partial class XZHeader
{
    public static async ValueTask<XZHeader> FromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var header = new XZHeader(new BinaryReader(stream, Encoding.UTF8, true));
        await header.ProcessAsync(cancellationToken).ConfigureAwait(false);
        return header;
    }

    public async ValueTask ProcessAsync(CancellationToken cancellationToken = default)
    {
        CheckMagicBytes(await _reader.ReadBytesAsync(6, cancellationToken).ConfigureAwait(false));
        await ProcessStreamFlagsAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ProcessStreamFlagsAsync(CancellationToken cancellationToken = default)
    {
        var streamFlags = await _reader.ReadBytesAsync(2, cancellationToken).ConfigureAwait(false);
        var crc = await _reader
            .BaseStream.ReadLittleEndianUInt32Async(cancellationToken)
            .ConfigureAwait(false);
        var calcCrc = Crc32.Compute(streamFlags);
        if (crc != calcCrc)
        {
            throw new InvalidFormatException("Stream header corrupt");
        }

        BlockCheckType = (CheckType)(streamFlags[1] & 0x0F);
        var futureUse = (byte)(streamFlags[1] & 0xF0);
        if (futureUse != 0 || streamFlags[0] != 0)
        {
            throw new InvalidFormatException("Unknown XZ Stream Version");
        }
    }
}
