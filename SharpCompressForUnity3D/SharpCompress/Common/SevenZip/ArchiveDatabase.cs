namespace SharpCompress.Common.SevenZip
{
    using SharpCompress.Compressor.LZMA;
    using SharpCompress.Compressor.LZMA.Utilites;
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class ArchiveDatabase
    {
        internal long DataStartPosition;
        internal List<int> FileIndexToFolderIndexMap = new List<int>();
        internal List<CFileItem> Files = new List<CFileItem>();
        internal List<CFolder> Folders = new List<CFolder>();
        internal List<int> FolderStartFileIndex = new List<int>();
        internal byte MajorVersion;
        internal byte MinorVersion;
        internal List<int> NumUnpackStreamsVector;
        internal List<uint?> PackCRCs = new List<uint?>();
        internal List<long> PackSizes = new List<long>();
        internal List<long> PackStreamStartPositions = new List<long>();
        internal long StartPositionAfterHeader;

        internal void Clear()
        {
            this.PackSizes.Clear();
            this.PackCRCs.Clear();
            this.Folders.Clear();
            this.NumUnpackStreamsVector = null;
            this.Files.Clear();
            this.PackStreamStartPositions.Clear();
            this.FolderStartFileIndex.Clear();
            this.FileIndexToFolderIndexMap.Clear();
        }

        public void Fill()
        {
            this.FillStartPos();
            this.FillFolderStartFileIndex();
        }

        private void FillFolderStartFileIndex()
        {
            this.FolderStartFileIndex.Clear();
            this.FileIndexToFolderIndexMap.Clear();
            int num = 0;
            int num2 = 0;
            for (int i = 0; i < this.Files.Count; i++)
            {
                bool flag2;
                CFileItem item = this.Files[i];
                bool flag = !item.HasStream;
                if (flag && (num2 == 0))
                {
                    this.FileIndexToFolderIndexMap.Add(-1);
                    continue;
                }
                if (num2 != 0)
                {
                    goto Label_00BF;
                }
                goto Label_00B9;
            Label_0075:
                if (num >= this.Folders.Count)
                {
                    throw new InvalidOperationException();
                }
                this.FolderStartFileIndex.Add(i);
                if (this.NumUnpackStreamsVector[num] != 0)
                {
                    goto Label_00BF;
                }
                num++;
            Label_00B9:
                flag2 = true;
                goto Label_0075;
            Label_00BF:
                this.FileIndexToFolderIndexMap.Add(num);
                if (!flag)
                {
                    num2++;
                    if (num2 >= this.NumUnpackStreamsVector[num])
                    {
                        num++;
                        num2 = 0;
                    }
                }
            }
        }

        private void FillStartPos()
        {
            this.PackStreamStartPositions.Clear();
            long item = 0L;
            for (int i = 0; i < this.PackSizes.Count; i++)
            {
                this.PackStreamStartPositions.Add(item);
                item += this.PackSizes[i];
            }
        }

        private long GetFilePackSize(int fileIndex)
        {
            int folderIndex = this.FileIndexToFolderIndexMap[fileIndex];
            if ((folderIndex != -1) && (this.FolderStartFileIndex[folderIndex] == fileIndex))
            {
                return this.GetFolderFullPackSize(folderIndex);
            }
            return 0L;
        }

        internal long GetFolderFullPackSize(int folderIndex)
        {
            int firstPackStreamId = this.Folders[folderIndex].FirstPackStreamId;
            CFolder folder = this.Folders[folderIndex];
            long num2 = 0L;
            for (int i = 0; i < folder.PackStreams.Count; i++)
            {
                num2 += this.PackSizes[firstPackStreamId + i];
            }
            return num2;
        }

        private long GetFolderPackStreamSize(int folderIndex, int streamIndex)
        {
            return this.PackSizes[this.Folders[folderIndex].FirstPackStreamId + streamIndex];
        }

        internal Stream GetFolderStream(Stream stream, CFolder folder, IPasswordProvider pw)
        {
            int firstPackStreamId = folder.FirstPackStreamId;
            long folderStreamPos = this.GetFolderStreamPos(folder, 0);
            List<long> list = new List<long>();
            for (int i = 0; i < folder.PackStreams.Count; i++)
            {
                list.Add(this.PackSizes[firstPackStreamId + i]);
            }
            return DecoderStreamHelper.CreateDecoderStream(stream, folderStreamPos, list.ToArray(), folder, pw);
        }

        internal long GetFolderStreamPos(CFolder folder, int indexInFolder)
        {
            int num = folder.FirstPackStreamId + indexInFolder;
            return (this.DataStartPosition + this.PackStreamStartPositions[num]);
        }

        internal bool IsEmpty()
        {
            return ((((this.PackSizes.Count == 0) && (this.PackCRCs.Count == 0)) && ((this.Folders.Count == 0) && (this.NumUnpackStreamsVector.Count == 0))) && (this.Files.Count == 0));
        }
    }
}

