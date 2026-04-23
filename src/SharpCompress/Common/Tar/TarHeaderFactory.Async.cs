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

        // In streaming mode we track the previous entry's packed stream so we can
        // advance past its data (and alignment padding) before reading the next header.
        TarReadOnlySubStream? previousPackedStream = null;
        while (true)
        {
            TarHeader? header = null;
            try
            {
                // Dispose the previous packed stream so any unread entry data and its
                // 512-byte alignment padding are skipped before we read the next header.
                if (previousPackedStream != null)
                {
#if LEGACY_DOTNET
                    previousPackedStream.Dispose();
#else
                    await previousPackedStream.DisposeAsync().ConfigureAwait(false);
#endif
                    previousPackedStream = null;
                }

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
                            var packedStream = new TarReadOnlySubStream(
                                stream,
                                header.Size,
                                useSyncOverAsync
                            );
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
}
