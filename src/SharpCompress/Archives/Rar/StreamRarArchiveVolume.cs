using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar;

internal class StreamRarArchiveVolume : RarVolume
{
    private readonly bool _isMultiVolume;

    internal StreamRarArchiveVolume(
        Stream stream,
        ReaderOptions options,
        int index,
        bool isMultiVolume
    )
        : base(StreamingMode.Seekable, stream, options, index)
    {
        _isMultiVolume = isMultiVolume;
    }

    internal override IEnumerable<RarFilePart> ReadFileParts() => GetVolumeFileParts();

    internal override RarFilePart CreateFilePart(MarkHeader markHeader, FileHeader fileHeader) =>
        new SeekableRarFilePart(
            markHeader,
            fileHeader,
            Index,
            Stream,
            ReaderOptions.Password,
            _isMultiVolume
        );
}
