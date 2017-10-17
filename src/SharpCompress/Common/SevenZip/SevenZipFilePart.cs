using System;
using System.IO;
using System.Linq;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip
{
    internal class SevenZipFilePart : FilePart
    {
        private CompressionType? _type;
        private readonly Stream _stream;
        private readonly ArchiveDatabase _database;

        internal SevenZipFilePart(Stream stream, ArchiveDatabase database, int index, CFileItem fileEntry, ArchiveEncoding archiveEncoding)
           : base(archiveEncoding)
        {
            this._stream = stream;
            this._database = database;
            Index = index;
            Header = fileEntry;
            if (Header.HasStream)
            {
                Folder = database.Folders[database.FileIndexToFolderIndexMap[index]];
            }
        }

        internal Stream BaseStream { get; private set; }
        internal CFileItem Header { get; }
        internal CFolder Folder { get; }
        internal int Index { get; }

        internal override string FilePartName => Header.Name;

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
            var folderStream = _database.GetFolderStream(_stream, Folder, null);

            int firstFileIndex = _database.FolderStartFileIndex[_database.Folders.IndexOf(Folder)];
            int skipCount = Index - firstFileIndex;
            long skipSize = 0;
            for (int i = 0; i < skipCount; i++)
            {
                skipSize += _database.Files[firstFileIndex + i].Size;
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
                if (_type == null)
                {
                    _type = GetCompression();
                }
                return _type.Value;
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