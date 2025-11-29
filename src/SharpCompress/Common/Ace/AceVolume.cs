using System.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Ace;

/// <summary>
/// Represents a volume (file) of an ACE archive.
/// </summary>
public class AceVolume : Volume
{
    public AceVolume(Stream stream, ReaderOptions readerOptions, int index = 0)
        : base(stream, readerOptions, index) { }
}
