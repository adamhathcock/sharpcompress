using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Rar;

namespace SharpCompress.Archives.Rar;

public class RarArchive : AbstractArchive<RarArchiveEntry, RarVolume>
{
    internal Lazy<IRarUnpack> UnpackV2017 { get; } =
        new Lazy<IRarUnpack>(() => new Compressors.Rar.UnpackV2017.Unpack());
    internal Lazy<IRarUnpack> UnpackV1 { get; } =
        new Lazy<IRarUnpack>(() => new Compressors.Rar.UnpackV1.Unpack());

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="srcStream"></param>
    /// <param name="options"></param>
    internal RarArchive(SourceStream srcStream) : base(ArchiveType.Rar, srcStream) { }

    protected override IEnumerable<RarArchiveEntry> LoadEntries(IEnumerable<RarVolume> volumes) =>
        RarArchiveEntryFactory.GetEntries(this, volumes, ReaderOptions);

    protected override IEnumerable<RarVolume> LoadVolumes(SourceStream srcStream)
    {
        SrcStream.LoadAllParts(); //request all streams
        var streams = SrcStream.Streams.ToArray();
        var idx = 0;
        if (streams.Length > 1 && IsRarFile(streams[1], ReaderOptions)) //test part 2 - true = multipart not split
        {
            SrcStream.IsVolumes = true;
            streams[1].Position = 0;
            SrcStream.Position = 0;

            return srcStream.Streams.Select(
                a => new StreamRarArchiveVolume(a, ReaderOptions, idx++)
            );
        }
        else //split mode or single file
        {
            return new StreamRarArchiveVolume(SrcStream, ReaderOptions, idx++).AsEnumerable();
        }
    }

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.First().Stream;
        stream.Position = 0;
        return RarReader.Open(stream, ReaderOptions);
    }

    public override bool IsSolid => Volumes.First().IsSolidArchive;

    public virtual int MinVersion => Volumes.First().MinVersion;
    public virtual int MaxVersion => Volumes.First().MaxVersion;

    #region Creation
    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="options"></param>
    public static RarArchive Open(string filePath, ReaderOptions? options = null)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));
        var fileInfo = new FileInfo(filePath);
        return new RarArchive(
            new SourceStream(
                fileInfo,
                i => RarArchiveVolumeFactory.GetFilePart(i, fileInfo),
                options ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="options"></param>
    public static RarArchive Open(FileInfo fileInfo, ReaderOptions? options = null)
    {
        fileInfo.CheckNotNull(nameof(fileInfo));
        return new RarArchive(
            new SourceStream(
                fileInfo,
                i => RarArchiveVolumeFactory.GetFilePart(i, fileInfo),
                options ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    public static RarArchive Open(Stream stream, ReaderOptions? options = null)
    {
        stream.CheckNotNull(nameof(stream));
        return new RarArchive(new SourceStream(stream, i => null, options ?? new ReaderOptions()));
    }

    /// <summary>
    /// Constructor with all file parts passed in
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    public static RarArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.CheckNotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new RarArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all stream parts passed in
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    public static RarArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.CheckNotNull(nameof(streams));
        var strms = streams.ToArray();
        return new RarArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    public static bool IsRarFile(string filePath) => IsRarFile(new FileInfo(filePath));

    public static bool IsRarFile(FileInfo fileInfo)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsRarFile(stream);
    }

    public static bool IsRarFile(Stream stream, ReaderOptions? options = null)
    {
        try
        {
            MarkHeader.Read(stream, true, false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
