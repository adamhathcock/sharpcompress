using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Zip;

public class ZipVolume : Volume
{
    public ZipVolume(Stream stream, ReaderOptions readerOptions, int index = 0)
        : base(stream, readerOptions, index) { }

    public string? Comment { get; internal set; }
}
