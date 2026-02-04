using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar;

internal static partial class TarHeaderFactory
{
    internal static async IAsyncEnumerable<TarHeader?> ReadHeaderAsync(
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
                var reader = new AsyncBinaryReader(stream, false);
                header = new TarHeader(archiveEncoding);
                if (!await header.ReadAsync(reader))
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
                            var useSyncOverAsync = false;
                            #if LEGACY_DOTNET
                                useSyncOverAsync = true;
                            #endif
                            header.PackedStream = new TarReadOnlySubStream(stream, header.Size, useSyncOverAsync);
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
}
