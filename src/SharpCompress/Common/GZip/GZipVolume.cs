using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.GZip;

public class GZipVolume : Volume
{
    public GZipVolume(Stream stream, ReaderOptions? options, int index)
        : base(stream, options, index) { }

    public GZipVolume(FileInfo fileInfo, ReaderOptions options)
        : base(fileInfo.OpenRead(), options) => options.LeaveStreamOpen = false;

    public override bool IsFirstVolume => true;

    public override bool IsMultiVolume => true;
}
