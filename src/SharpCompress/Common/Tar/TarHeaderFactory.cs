using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar;

internal static partial class TarHeaderFactory
{
    internal static IEnumerable<TarHeader?> ReadHeader(
        StreamingMode mode,
        Stream stream,
        IArchiveEncoding archiveEncoding
    )
    {
        while (true)
        {
            TarHeader? header = null;
            try
            {
                var reader = new BinaryReader(stream, archiveEncoding.Default, leaveOpen: false);
                header = new TarHeader(archiveEncoding);

                if (!header.Read(reader))
                {
                    yield break;
                }
                switch (mode)
                {
                    case StreamingMode.Seekable:
                        {
                            header.DataStartPosition = stream.Position;

                            //skip to nearest 512
                            stream.Position += PadTo512(header.Size);
                        }
                        break;
                    case StreamingMode.Streaming:
                        {
                            header.PackedStream = new TarReadOnlySubStream(stream, header.Size);
                        }
                        break;
                    default:
                    {
                        throw new InvalidFormatException("Invalid StreamingMode");
                    }
                }
            }
            catch
            {
                header = null;
            }
            yield return header;
        }
    }

    // Async methods moved to TarHeaderFactory.Async.cs

    private static long PadTo512(long size)
    {
        var zeros = (int)(size % 512);
        if (zeros == 0)
        {
            return size;
        }
        return 512 - zeros + size;
    }
}
