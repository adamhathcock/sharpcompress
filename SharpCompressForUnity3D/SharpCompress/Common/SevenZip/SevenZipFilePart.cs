namespace SharpCompress.Common.SevenZip
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    internal class SevenZipFilePart : FilePart
    {
        [CompilerGenerated]
        private Stream <BaseStream>k__BackingField;
        [CompilerGenerated]
        private CFolder <Folder>k__BackingField;
        [CompilerGenerated]
        private CFileItem <Header>k__BackingField;
        [CompilerGenerated]
        private int <Index>k__BackingField;
        private ArchiveDatabase database;
        private const uint k_BCJ = 0x3030103;
        private const uint k_BCJ2 = 0x303011b;
        private const uint k_BZip2 = 0x40202;
        private const uint k_Copy = 0;
        private const uint k_Deflate = 0x40108;
        private const uint k_Delta = 3;
        private const uint k_LZMA = 0x30101;
        private const uint k_LZMA2 = 0x21;
        private const uint k_PPMD = 0x30401;
        private Stream stream;
        private SharpCompress.Common.CompressionType? type;

        internal SevenZipFilePart(Stream stream, ArchiveDatabase database, int index, CFileItem fileEntry)
        {
            this.stream = stream;
            this.database = database;
            this.Index = index;
            this.Header = fileEntry;
            if (this.Header.HasStream)
            {
                this.Folder = database.Folders[database.FileIndexToFolderIndexMap[index]];
            }
        }

        internal override Stream GetCompressedStream()
        {
            if (!this.Header.HasStream)
            {
                return null;
            }
            Stream source = this.database.GetFolderStream(this.stream, this.Folder, null);
            int num = this.database.FolderStartFileIndex[this.database.Folders.IndexOf(this.Folder)];
            int num2 = this.Index - num;
            long advanceAmount = 0L;
            for (int i = 0; i < num2; i++)
            {
                advanceAmount += this.database.Files[num + i].Size;
            }
            if (advanceAmount > 0L)
            {
                Utility.Skip(source, advanceAmount);
            }
            return new ReadOnlySubStream(source, this.Header.Size);
        }

        internal SharpCompress.Common.CompressionType GetCompression()
        {
            switch (Enumerable.First<CCoderInfo>(this.Folder.Coders).MethodId.Id)
            {
                case 0x30401L:
                    return SharpCompress.Common.CompressionType.PPMd;

                case 0x40202L:
                    return SharpCompress.Common.CompressionType.BZip2;

                case 0x21L:
                case 0x30101L:
                    return SharpCompress.Common.CompressionType.LZMA;
            }
            throw new NotImplementedException();
        }

        internal override Stream GetRawStream()
        {
            return null;
        }

        internal Stream BaseStream
        {
            [CompilerGenerated]
            get
            {
                return this.<BaseStream>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<BaseStream>k__BackingField = value;
            }
        }

        public SharpCompress.Common.CompressionType CompressionType
        {
            get
            {
                if (!this.type.HasValue)
                {
                    this.type = new SharpCompress.Common.CompressionType?(this.GetCompression());
                }
                return this.type.Value;
            }
        }

        internal override string FilePartName
        {
            get
            {
                return this.Header.Name;
            }
        }

        internal CFolder Folder
        {
            [CompilerGenerated]
            get
            {
                return this.<Folder>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Folder>k__BackingField = value;
            }
        }

        internal CFileItem Header
        {
            [CompilerGenerated]
            get
            {
                return this.<Header>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Header>k__BackingField = value;
            }
        }

        internal int Index
        {
            [CompilerGenerated]
            get
            {
                return this.<Index>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Index>k__BackingField = value;
            }
        }
    }
}

