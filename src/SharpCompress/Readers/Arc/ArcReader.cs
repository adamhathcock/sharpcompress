using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Arc;

namespace SharpCompress.Readers.Arc;

public partial class ArcReader : AbstractReader<ArcEntry, ArcVolume>
{
    private ArcReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.Arc) => Volume = new ArcVolume(stream, options, 0);

    public override ArcVolume Volume { get; }

    /// <summary>
    /// Opens an ArcReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.RequireReadable();
        return new ArcReader(stream, readerOptions ?? ReaderOptions.ForExternalStream);
    }

    protected override IEnumerable<ArcEntry> GetEntries(Stream stream)
    {
        ArcEntryHeader headerReader = new ArcEntryHeader(Options.ArchiveEncoding);
        ArcEntryHeader? header;
        while ((header = headerReader.ReadHeader(stream)) != null)
        {
            yield return new ArcEntry(new ArcFilePart(header, stream), Options);
        }
    }
}
