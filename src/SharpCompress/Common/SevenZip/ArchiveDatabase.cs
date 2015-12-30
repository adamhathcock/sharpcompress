using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Compressor.LZMA;
using SharpCompress.Compressor.LZMA.Utilites;

namespace SharpCompress.Common.SevenZip
{
    internal class ArchiveDatabase
    {
        internal byte MajorVersion;
        internal byte MinorVersion;
        internal long StartPositionAfterHeader;
        internal long DataStartPosition;

        internal List<long> PackSizes = new List<long>();
        internal List<uint?> PackCRCs = new List<uint?>();
        internal List<CFolder> Folders = new List<CFolder>();
        internal List<int> NumUnpackStreamsVector;
        internal List<CFileItem> Files = new List<CFileItem>();

        internal List<long> PackStreamStartPositions = new List<long>();
        internal List<int> FolderStartFileIndex = new List<int>();
        internal List<int> FileIndexToFolderIndexMap = new List<int>();

        internal void Clear()
        {
            PackSizes.Clear();
            PackCRCs.Clear();
            Folders.Clear();
            NumUnpackStreamsVector = null;
            Files.Clear();

            PackStreamStartPositions.Clear();
            FolderStartFileIndex.Clear();
            FileIndexToFolderIndexMap.Clear();
        }

        internal bool IsEmpty()
        {
            return PackSizes.Count == 0
                   && PackCRCs.Count == 0
                   && Folders.Count == 0
                   && NumUnpackStreamsVector.Count == 0
                   && Files.Count == 0;
        }

        private void FillStartPos()
        {
            PackStreamStartPositions.Clear();

            long startPos = 0;
            for (int i = 0; i < PackSizes.Count; i++)
            {
                PackStreamStartPositions.Add(startPos);
                startPos += PackSizes[i];
            }
        }

        private void FillFolderStartFileIndex()
        {
            FolderStartFileIndex.Clear();
            FileIndexToFolderIndexMap.Clear();

            int folderIndex = 0;
            int indexInFolder = 0;
            for (int i = 0; i < Files.Count; i++)
            {
                CFileItem file = Files[i];

                bool emptyStream = !file.HasStream;

                if (emptyStream && indexInFolder == 0)
                {
                    FileIndexToFolderIndexMap.Add(-1);
                    continue;
                }

                if (indexInFolder == 0)
                {
                    // v3.13 incorrectly worked with empty folders
                    // v4.07: Loop for skipping empty folders
                    for (; ; )
                    {
                        if (folderIndex >= Folders.Count)
                            throw new InvalidOperationException();

                        FolderStartFileIndex.Add(i); // check it

                        if (NumUnpackStreamsVector[folderIndex] != 0)
                            break;

                        folderIndex++;
                    }
                }

                FileIndexToFolderIndexMap.Add(folderIndex);

                if (emptyStream)
                    continue;

                indexInFolder++;

                if (indexInFolder >= NumUnpackStreamsVector[folderIndex])
                {
                    folderIndex++;
                    indexInFolder = 0;
                }
            }
        }

        public void Fill()
        {
            FillStartPos();
            FillFolderStartFileIndex();
        }

        internal long GetFolderStreamPos(CFolder folder, int indexInFolder)
        {
            int index = folder.FirstPackStreamId + indexInFolder;
            return DataStartPosition + PackStreamStartPositions[index];
        }

        internal long GetFolderFullPackSize(int folderIndex)
        {
            int packStreamIndex = Folders[folderIndex].FirstPackStreamId;
            CFolder folder = Folders[folderIndex];

            long size = 0;
            for (int i = 0; i < folder.PackStreams.Count; i++)
                size += PackSizes[packStreamIndex + i];

            return size;
        }

        internal Stream GetFolderStream(Stream stream, CFolder folder, IPasswordProvider pw)
        {
            int packStreamIndex = folder.FirstPackStreamId;
            long folderStartPackPos = GetFolderStreamPos(folder, 0);
            List<long> packSizes = new List<long>();
            for (int j = 0; j < folder.PackStreams.Count; j++)
                packSizes.Add(PackSizes[packStreamIndex + j]);

            return DecoderStreamHelper.CreateDecoderStream(stream, folderStartPackPos, packSizes.ToArray(), folder, pw);
        }

        private long GetFolderPackStreamSize(int folderIndex, int streamIndex)
        {
            return PackSizes[Folders[folderIndex].FirstPackStreamId + streamIndex];
        }

        private long GetFilePackSize(int fileIndex)
        {
            int folderIndex = FileIndexToFolderIndexMap[fileIndex];
            if (folderIndex != -1)
                if (FolderStartFileIndex[folderIndex] == fileIndex)
                    return GetFolderFullPackSize(folderIndex);
            return 0;
        }
    }
}