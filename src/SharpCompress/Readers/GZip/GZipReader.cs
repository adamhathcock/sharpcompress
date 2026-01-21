using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.GZip;

namespace SharpCompress.Readers.GZip;

public partial class GZipReader : AbstractReader<GZipEntry, GZipVolume>
{
    private GZipReader(Stream stream, ReaderOptions options)
        : base(options, ArchiveType.GZip) => Volume = new GZipVolume(stream, options, 0);

    public override GZipVolume Volume { get; }

    #region OpenReader

    /// <summary>
    /// Opens a GZipReader for Non-seeking usage with a single volume
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IReader OpenReader(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        return new GZipReader(stream, options ?? new ReaderOptions());
    }
       public static IAsyncReader OpenAsyncReader(Stream stream, ReaderOptions? options = null)
    {
        stream.NotNull(nameof(stream));
        return new GZipReader(stream, options ?? new ReaderOptions());
    }

    #endregion OpenReader

    protected override IEnumerable<GZipEntry> GetEntries(Stream stream) =>
        GZipEntry.GetEntries(stream, Options);
    protected override IAsyncEnumerable<GZipEntry> GetEntriesAsync(Stream stream) {
        return GZipEntry.GetEntriesAsync(stream, Options);
    }
}
