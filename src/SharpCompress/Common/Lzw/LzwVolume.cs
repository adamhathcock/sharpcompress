using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Lzw;

public class LzwVolume : Volume
{
    public LzwVolume(Stream stream, ReaderOptions? options, int index)
        : base(stream, options, index) { }

    public LzwVolume(FileInfo fileInfo, ReaderOptions options)
        : base(fileInfo.OpenRead(), options with { LeaveStreamOpen = false }) { }

    public override bool IsFirstVolume => true;

    public override bool IsMultiVolume => false;
}
