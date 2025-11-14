using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.Xz;

public class XZHeader
{
    private readonly BinaryReader _reader;
    private readonly byte[] MagicHeader = { 0xFD, 0x37, 0x7A, 0x58, 0x5a, 0x00 };

    public CheckType BlockCheckType { get; private set; }
    public int BlockCheckSize => 4 << ((((int)BlockCheckType + 2) / 3) - 1);

    public XZHeader(BinaryReader reader) => _reader = reader;

    public static XZHeader FromStream(Stream stream)
    {
        var header = new XZHeader(new BinaryReader(stream, Encoding.UTF8, true));
        header.Process();
        return header;
    }

    public static async Task<XZHeader> FromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        var header = new XZHeader(new BinaryReader(stream, Encoding.UTF8, true));
        await header.ProcessAsync(cancellationToken).ConfigureAwait(false);
        return header;
    }

    public void Process()
    {
        CheckMagicBytes(_reader.ReadBytes(6));
        ProcessStreamFlags();
    }

    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        CheckMagicBytes(await _reader.ReadBytesAsync(6, cancellationToken).ConfigureAwait(false));
        await ProcessStreamFlagsAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ProcessStreamFlags()
    {
        var streamFlags = _reader.ReadBytes(2);
        var crc = _reader.ReadLittleEndianUInt32();
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

    private async Task ProcessStreamFlagsAsync(CancellationToken cancellationToken = default)
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

    private void CheckMagicBytes(byte[] header)
    {
        if (!header.SequenceEqual(MagicHeader))
        {
            throw new InvalidFormatException("Invalid XZ Stream");
        }
    }
}
