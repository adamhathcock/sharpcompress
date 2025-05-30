using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar;

/// <summary>
/// A rar part based on a FileInfo object
/// </summary>
internal class FileInfoRarArchiveVolume : RarVolume
{
    internal FileInfoRarArchiveVolume(FileInfo fileInfo, ReaderOptions options, int index)
        : base(StreamingMode.Seekable, fileInfo.OpenRead(), FixOptions(options), index)
    {
        FileInfo = fileInfo;
        FileParts = GetVolumeFileParts().ToArray().ToReadOnly();
    }

    private static ReaderOptions FixOptions(ReaderOptions options)
    {
        //make sure we're closing streams with fileinfo
        options.LeaveStreamOpen = false;
        return options;
    }

    internal ReadOnlyCollection<RarFilePart> FileParts { get; }

    internal FileInfo FileInfo { get; }

    internal override RarFilePart CreateFilePart(MarkHeader markHeader, FileHeader fileHeader) =>
        new FileInfoRarFilePart(this, ReaderOptions.Password, markHeader, fileHeader, FileInfo);

    internal override IEnumerable<RarFilePart> ReadFileParts() => FileParts;
}
