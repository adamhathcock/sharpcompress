using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.SevenZip;

public class SevenZipVolume : Volume
{
    public SevenZipVolume(Stream stream, ReaderOptions readerFactoryOptions, int index = 0)
        : base(stream, readerFactoryOptions, index) { }
}
