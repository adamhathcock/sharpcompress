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
#if NET8_0_OR_GREATER
        await using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#else
        using var reader = new AsyncBinaryReader(stream, leaveOpen: true);
#endif

        while (true)
        {
            TarHeader? header = null;
            try
            {
                header = new TarHeader(archiveEncoding);
                if (!await header.ReadAsync(reader).ConfigureAwait(false))
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
                            header.PackedStream = new TarReadOnlySubStream(
                                stream,
                                header.Size,
                                useSyncOverAsync
                            );
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
