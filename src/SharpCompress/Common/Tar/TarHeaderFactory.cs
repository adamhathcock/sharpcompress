using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Tar
{
    internal static class TarHeaderFactory
    {
        internal static async IAsyncEnumerable<TarHeader?> ReadHeader(StreamingMode mode, Stream stream, ArchiveEncoding archiveEncoding, 
                                                                      [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            while (true)
            {
                TarHeader? header = null;
                try
                {
                    header = new TarHeader(archiveEncoding);

                    if (!await header.Read(stream, cancellationToken))
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

        private static long PadTo512(long size)
        {
            int zeros = (int)(size % 512);
            if (zeros == 0)
            {
                return size;
            }
            return 512 - zeros + size;
        }
    }
}