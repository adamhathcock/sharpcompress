namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using SharpCompress.Common.Zip.Headers;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using SharpCompress.IO;

    internal class SeekableZipHeaderFactory : ZipHeaderFactory
    {
        private const int MAX_ITERATIONS_FOR_DIRECTORY_HEADER = 0x3e8;

        internal SeekableZipHeaderFactory(string password) : base(StreamingMode.Seekable, password)
        {
        }

        internal LocalEntryHeader GetLocalHeader(Stream stream, DirectoryEntryHeader directoryEntryHeader)
        {
            stream.Seek((long) directoryEntryHeader.RelativeOffsetOfEntryHeader, SeekOrigin.Begin);
            BinaryReader reader = new BinaryReader(stream);
            uint headerBytes = reader.ReadUInt32();
            LocalEntryHeader header = base.ReadHeader(headerBytes, reader) as LocalEntryHeader;
            if (header == null)
            {
                throw new InvalidOperationException();
            }
            return header;
        }

        internal IEnumerable<DirectoryEntryHeader> ReadSeekableHeader(Stream stream)
        {
            uint iteratorVariable1;
            long iteratorVariable0 = 0L;
            BinaryReader reader = new BinaryReader(stream);
            int iteratorVariable3 = 0;
            do
            {
                if (((stream.Length + iteratorVariable0) - 4L) < 0L)
                {
                    throw new ArchiveException("Failed to locate the Zip Header");
                }
                stream.Seek(iteratorVariable0 - 4L, SeekOrigin.End);
                iteratorVariable1 = reader.ReadUInt32();
                iteratorVariable0 -= 1L;
                iteratorVariable3++;
                if (iteratorVariable3 > 0x3e8)
                {
                    throw new ArchiveException("Could not find Zip file Directory at the end of the file.  File may be corrupted.");
                }
            }
            while (iteratorVariable1 != 0x6054b50);
            DirectoryEndHeader iteratorVariable4 = new DirectoryEndHeader();
            iteratorVariable4.Read(reader);
            stream.Seek((long) iteratorVariable4.DirectoryStartOffsetRelativeToDisk, SeekOrigin.Begin);
            DirectoryEntryHeader iteratorVariable5 = null;
            long position = stream.Position;
            while (true)
            {
                stream.Position = position;
                iteratorVariable1 = reader.ReadUInt32();
                iteratorVariable5 = this.ReadHeader(iteratorVariable1, reader) as DirectoryEntryHeader;
                position = stream.Position;
                if (iteratorVariable5 == null)
                {
                    yield break;
                }
                iteratorVariable5.HasData = iteratorVariable5.CompressedSize != 0;
                yield return iteratorVariable5;
            }
        }

    }
}

