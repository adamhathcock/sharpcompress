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
        // In streaming mode we track the previous entry's packed stream so we can
        // advance past its data (and alignment padding) before reading the next header.
        // Without this, the loop would re-read entry data bytes as a header.
        TarReadOnlySubStream? previousPackedStream = null;
        while (true)
        {
            TarHeader? header = null;
            try
            {
                // Dispose the previous packed stream so any unread entry data and its
                // 512-byte alignment padding are skipped before we read the next header.
                previousPackedStream?.Dispose();
                previousPackedStream = null;

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
                            var packedStream = new TarReadOnlySubStream(stream, header.Size, false);
                            header.PackedStream = packedStream;
                            previousPackedStream = packedStream;
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
