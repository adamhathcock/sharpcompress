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
        var globalPaxMetadata = new TarHeader.PaxMetadata();
        while (true)
        {
            TarHeader? header = null;
            try
            {
                var reader = new BinaryReader(stream, archiveEncoding.Default, leaveOpen: false);
                header = new TarHeader(archiveEncoding);

                if (!header.Read(reader, globalPaxMetadata))
                {
                    yield break;
                }
                switch (mode)
                {
                    case StreamingMode.Seekable:
                        {
                            header.DataStartPosition = stream.Position;

                            // Reject padded sizes or end offsets that would wrap into a backward seek.
                            stream.Position = checked(stream.Position + PadTo512(header.Size));
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
        // PAX size overrides bypass ReadSize, so validate the effective size too.
        if (size is < 0 or > TarHeader.MAX_ENTRY_SIZE)
        {
            throw new InvalidFormatException($"Invalid TAR entry size: {size}.");
        }

        return (size + 511) & ~511L;
    }
}
