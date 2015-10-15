namespace SharpCompress.Common.Tar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Tar.Headers;
    using SharpCompress.IO;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;

    internal static class TarHeaderFactory
    {
        private static long PadTo512(long size)
        {
            int num = (int) (size % 0x200L);
            if (num == 0)
            {
                return size;
            }
            return ((0x200 - num) + size);
        }

        internal static IEnumerable<TarHeader> ReadHeader(StreamingMode mode, Stream stream)
        {
            TarHeader iteratorVariable0;
            bool flag2;
            goto Label_012E;
        Label_010E:
            yield return iteratorVariable0;
        Label_012E:
            flag2 = true;
            iteratorVariable0 = null;
            try
            {
                BinaryReader reader = new BinaryReader(stream);
                iteratorVariable0 = new TarHeader();
                if (iteratorVariable0.Read(reader))
                {
                    switch (mode)
                    {
                        case StreamingMode.Streaming:
                            iteratorVariable0.PackedStream = new TarReadOnlySubStream(stream, iteratorVariable0.Size);
                            goto Label_010E;

                        case StreamingMode.Seekable:
                        {
                            iteratorVariable0.DataStartPosition = new long?(reader.BaseStream.Position);
                            Stream baseStream = reader.BaseStream;
                            baseStream.Position += PadTo512(iteratorVariable0.Size);
                            goto Label_010E;
                        }
                    }
                    throw new InvalidFormatException("Invalid StreamingMode");
                }
                goto Label_0135;
            }
            catch
            {
                iteratorVariable0 = null;
            }
            goto Label_010E;
        Label_0135:;
        }

    }
}

