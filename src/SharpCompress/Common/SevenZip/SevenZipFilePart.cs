using System;
using System.IO;
using System.Linq;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip
{
    internal class SevenZipFilePart : FilePart
    {
        private CompressionType? type;
        private Stream stream;
        private ArchiveDatabase database;

        internal SevenZipFilePart(Stream stream, ArchiveDatabase database, int index, CFileItem fileEntry)
        {
            this.stream = stream;
            this.database = database;
            Index = index;
            Header = fileEntry;
            if (Header.HasStream)
            {
                Folder = database.Folders[database.FileIndexToFolderIndexMap[index]];
            }
        }

        internal Stream BaseStream { get; private set; }
        internal CFileItem Header { get; private set; }
        internal CFolder Folder { get; private set; }
        internal int Index { get; private set; }

        internal override string FilePartName
        {
            get { return Header.Name; }
        }

        internal override Stream GetRawStream()
        {
            return null;
        }

        internal override Stream GetCompressedStream()
        {
            if (!Header.HasStream)
            {
                return null;
            }
            var folderStream = database.GetFolderStream(stream, Folder, null);

            int firstFileIndex = database.FolderStartFileIndex[database.Folders.IndexOf(Folder)];
            int skipCount = Index - firstFileIndex;
            long skipSize = 0;
            for (int i = 0; i < skipCount; i++)
            {
                skipSize += database.Files[firstFileIndex + i].Size;
            }
            if (skipSize > 0)
            {
                folderStream.Skip(skipSize);
            }
            return new ReadOnlySubStream(folderStream, Header.Size);
        }

        public CompressionType CompressionType
        {
            get
            {
                if (type == null)
                {
                    type = GetCompression();
                }
                return type.Value;
            }
        }

        //copied from DecoderRegistry
        private const uint k_Copy = 0x0;
        private const uint k_Delta = 3;
        private const uint k_LZMA2 = 0x21;
        private const uint k_LZMA = 0x030101;
        private const uint k_PPMD = 0x030401;
        private const uint k_BCJ = 0x03030103;
        private const uint k_BCJ2 = 0x0303011B;
        private const uint k_Deflate = 0x040108;
        private const uint k_BZip2 = 0x040202;

        internal CompressionType GetCompression()
        {
            var coder = Folder.Coders.First();
            switch (coder.MethodId.Id)
            {
                case k_LZMA:
                case k_LZMA2:
                    {
                        return CompressionType.LZMA;
                    }
                case k_PPMD:
                    {
                        return CompressionType.PPMd;
                    }
                case k_BZip2:
                    {
                        return CompressionType.BZip2;
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}