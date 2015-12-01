namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.IO;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;

    internal class StreamingZipHeaderFactory : ZipHeaderFactory
    {
        internal StreamingZipHeaderFactory(string password) : base(StreamingMode.Streaming, password)
        {
        }

        internal IEnumerable<ZipHeader> ReadStreamHeader(Stream stream)
        {
            RewindableStream iteratorVariable0;
            if (stream is RewindableStream)
            {
                iteratorVariable0 = stream as RewindableStream;
            }
            else
            {
                iteratorVariable0 = new RewindableStream(stream);
            }
            while (true)
            {
                ZipHeader iteratorVariable1 = null;
                BinaryReader reader = new BinaryReader(iteratorVariable0);
                if ((this.lastEntryHeader != null) && FlagUtility.HasFlag<HeaderFlags>(this.lastEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor))
                {
                    reader = (this.lastEntryHeader.Part as StreamingZipFilePart).FixStreamedFileLocation(ref iteratorVariable0);
                    long position = iteratorVariable0.Position;
                    uint num2 = reader.ReadUInt32();
                    if (num2 == 0x8074b50)
                    {
                        num2 = reader.ReadUInt32();
                    }
                    this.lastEntryHeader.Crc = num2;
                    this.lastEntryHeader.CompressedSize = reader.ReadUInt32();
                    this.lastEntryHeader.UncompressedSize = reader.ReadUInt32();
                    this.lastEntryHeader.DataStartPosition = new long?(position - this.lastEntryHeader.CompressedSize);
                }
                this.lastEntryHeader = null;
                uint iteratorVariable3 = reader.ReadUInt32();
                iteratorVariable1 = this.ReadHeader(iteratorVariable3, reader);
                if (iteratorVariable1.ZipHeaderType == ZipHeaderType.LocalEntry)
                {
                    bool isRecording = iteratorVariable0.IsRecording;
                    if (!isRecording)
                    {
                        iteratorVariable0.StartRecording();
                    }
                    uint headerBytes = reader.ReadUInt32();
                    iteratorVariable1.HasData = !ZipHeaderFactory.IsHeader(headerBytes);
                    iteratorVariable0.Rewind(!isRecording);
                }
                yield return iteratorVariable1;
            }
        }

    }
}

