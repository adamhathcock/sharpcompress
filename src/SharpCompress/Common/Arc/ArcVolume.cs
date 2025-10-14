using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Arc;

public class ArcVolume : Volume
{
  public ArcVolume(Stream stream, ReaderOptions readerOptions, int index = 0)
    : base(stream, readerOptions, index) { }
}
