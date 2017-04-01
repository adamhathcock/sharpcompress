using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Decoder = SharpCompress.Compressor.LZMA.Decoder;

namespace SharpCompress.Common.SevenZip
{
    internal class SevenZipHeaderFactory
    {
        private Stream stream;

        private static readonly byte[] SIGNATURE = new byte[] { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

        public SevenZipHeaderFactory(Stream stream)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new NotSupportedException("Need a readable and seekable stream.");
            }
            this.stream = stream;
            Initialize();
        }

        public StreamsInfo ArchiveInfo { get; set; }
        public StreamsInfo FilesInfo { get; set; }
        public HeaderEntry[] Entries { get; set; }
        public long BaseOffset { get; set; }

        public Stream BaseStream
        {
            get
            {
                return stream;
            }
        }

        public long GetFolderFullPackSize(int folderIndex)
        {
            Folder folder = FilesInfo.Folders[folderIndex];
            return folder.PackedStreams.Select(x => x.PackedSize).Sum(x => (long)x);
        }

        public ulong GetFolderStreamPos(int folderIndex, int indexInFolder)
        {
            Folder folder = FilesInfo.Folders[folderIndex];
            return (ulong)BaseOffset + folder.PackedStreams[indexInFolder].StartPosition;
        }

        private void PostProcess()
        {
            int startPos = 0;
            ulong startPosSize = 0;
            for (int i = 0; i < FilesInfo.Folders.Length; i++)
            {
                FilesInfo.Folders[i].PackedStreams = new PackedStreamInfo[FilesInfo.Folders[i].PackedStreamIndices.Length];
                FilesInfo.Folders[i].PackedStreams.Initialize(() => FilesInfo.PackedStreams[startPos++]);
            }

            for (int i = 0; i < FilesInfo.PackedStreams.Length; i++)
            {
                FilesInfo.PackedStreams[i].StartPosition = startPosSize;
                startPosSize += FilesInfo.PackedStreams[i].PackedSize;
            }

            int unpackedStreamsIndex = 0;
            ulong folderOffset = 0;
            var folders = FilesInfo.Folders.Where(x => x.UnpackedStreams.Length != 0).GetEnumerator();

            foreach (var entry in Entries.Where(x => x.HasStream))
            {
                if ((folders.Current == null) || (unpackedStreamsIndex >= folders.Current.UnpackedStreams.Length))
                {
                    if (!folders.MoveNext())
                    {
                        throw new InvalidOperationException();
                    }
                    unpackedStreamsIndex = 0;
                    folderOffset = 0;
                }
                entry.Folder = folders.Current;
                entry.UnpackedStream = folders.Current.UnpackedStreams[unpackedStreamsIndex];
                entry.FolderOffset = folderOffset;
                unpackedStreamsIndex++;
                folderOffset += entry.UnpackedStream.UnpackedSize;
            }
        }

        public static bool SignatureMatch(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            byte[] signatureBytes = reader.ReadBytes(6);
            return signatureBytes.BinaryEquals(SIGNATURE);
        }

        private void Initialize()
        {
            if (!SignatureMatch(stream))
            {
                throw new InvalidFormatException("Not a 7Zip archive.");
            }
            BinaryReader reader = new BinaryReader(stream);
            reader.ReadByte();//major
            reader.ReadByte();//minor
            uint crc = reader.ReadUInt32();
            ulong nextHeaderOffset = reader.ReadUInt64();
            ulong nextHeaderSize = reader.ReadUInt64();
            uint nextHeaderCRC = reader.ReadUInt32();
            BaseOffset = stream.Position;

            stream.Seek(BaseOffset + (long)nextHeaderOffset, SeekOrigin.Begin);
            var headerBytes = new HeaderBuffer();
            headerBytes.Bytes = reader.ReadBytes((int)nextHeaderSize);
            ReadArchive(headerBytes);
            PostProcess();
        }

        private void ReadArchive(HeaderBuffer headerBytes)
        {
            while (true)
            {
                var prop = headerBytes.ReadProperty();
                switch (prop)
                {
                    case HeaderProperty.kEncodedHeader:
                        {
                            ArchiveInfo = ReadPackedStreams(headerBytes);
                            stream.Seek((long)ArchiveInfo.PackPosition + BaseOffset, SeekOrigin.Begin);
                            var firstFolder = ArchiveInfo.Folders.First();

                            ulong unpackSize = firstFolder.GetUnpackSize();

                            ulong packSize = ArchiveInfo.PackedStreams.Select(x => x.PackedSize)
                                .Aggregate((ulong)0, (sum, size) => sum + size);

                            byte[] unpackedBytes = new byte[(int)unpackSize];
                            Decoder decoder = new Decoder();
                            decoder.SetDecoderProperties(firstFolder.Coders[0].Properties);
                            using (MemoryStream outStream = new MemoryStream(unpackedBytes))
                            {
                               decoder.Code(stream, outStream, (long)(packSize), (long)unpackSize, null);
                            }

                            headerBytes = new HeaderBuffer { Bytes = unpackedBytes };
                        }
                        break;
                    case HeaderProperty.kHeader:
                        {
                            ReadFileHeader(headerBytes);
                            return;
                        }
                    default:
                        throw new NotSupportedException("7Zip header " + prop);
                }
            }
        }

        private void ReadFileHeader(HeaderBuffer headerBytes)
        {
            while (true)
            {
                var prop = headerBytes.ReadProperty();
                switch (prop)
                {
                    case HeaderProperty.kMainStreamsInfo:
                        {
                            FilesInfo = ReadPackedStreams(headerBytes);
                        }
                        break;
                    case HeaderProperty.kFilesInfo:
                        {
                            Entries = ReadFilesInfo(FilesInfo, headerBytes);
                        }
                        break;
                    case HeaderProperty.kEnd:
                        return;
                    default:
                        throw new InvalidFormatException(prop.ToString());
                }
            }
        }
        private static HeaderEntry[] ReadFilesInfo(StreamsInfo info, HeaderBuffer headerBytes)
        {
            var entries = headerBytes.CreateArray<HeaderEntry>();
            int numEmptyStreams = 0;
            while (true)
            {
                var type = headerBytes.ReadProperty();
                if (type == HeaderProperty.kEnd)
                {
                    break;
                }

                var size = (int)headerBytes.ReadEncodedInt64();

                switch (type)
                {
                    case HeaderProperty.kName:
                        {
                            if (headerBytes.ReadByte() != 0)
                            {
                                throw new InvalidFormatException("Cannot be external");
                            }
                            entries.ForEach(f => f.Name = headerBytes.ReadName());
                            break;
                        }
                    case HeaderProperty.kEmptyStream:
                        {
                            info.EmptyStreamFlags = headerBytes.ReadBoolFlags(entries.Length);
                            numEmptyStreams = info.EmptyStreamFlags.Where(x => x).Count();
                            break;
                        }
                    case HeaderProperty.kEmptyFile: //just read bytes
                    case HeaderProperty.kAnti:
                        {
                            info.EmptyFileFlags = headerBytes.ReadBoolFlags(numEmptyStreams);
                            break;
                        }
                    default:
                        {
                            headerBytes.ReadBytes(size);
                            break;
                        }
                }
            }
            int emptyFileIndex = 0;
            int sizeIndex = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                HeaderEntry file = entries[i];
                file.IsAnti = false;
                if (info.EmptyStreamFlags == null)
                {
                    file.HasStream = true;
                }
                else
                {
                    file.HasStream = !info.EmptyStreamFlags[i];
                }
                if (file.HasStream)
                {
                    file.IsDirectory = false;
                    file.Size = info.UnpackedStreams[sizeIndex].UnpackedSize;
                    file.FileCRC = info.UnpackedStreams[sizeIndex].Digest;
                    sizeIndex++;
                }
                else
                {
                    if (info.EmptyFileFlags == null)
                    {
                        file.IsDirectory = true;
                    }
                    else
                    {
                        file.IsDirectory = !info.EmptyFileFlags[emptyFileIndex];
                    }
                    emptyFileIndex++;
                    file.Size = 0;
                }
            }
            return entries;
        }
        private StreamsInfo ReadPackedStreams(HeaderBuffer headerBytes)
        {
            StreamsInfo info = new StreamsInfo();
            while (true)
            {
                var prop = headerBytes.ReadProperty();
                switch (prop)
                {
                    case HeaderProperty.kUnPackInfo:
                        {
                            ReadUnPackInfo(info, headerBytes);
                        }
                        break;
                    case HeaderProperty.kPackInfo:
                        {
                            ReadPackInfo(info, headerBytes);
                        }
                        break;
                    case HeaderProperty.kSubStreamsInfo:
                        {
                            ReadSubStreamsInfo(info, headerBytes);
                        }
                        break;
                    case HeaderProperty.kEnd:
                        return info;
                    default:
                        throw new InvalidFormatException(prop.ToString());
                }
            }
        }

        private static void ReadSubStreamsInfo(StreamsInfo info, HeaderBuffer headerBytes)
        {
            info.UnpackedStreams = new List<UnpackedStreamInfo>();
            foreach (var folder in info.Folders)
            {
                folder.UnpackedStreams = new UnpackedStreamInfo[1];
                folder.UnpackedStreams[0] = new UnpackedStreamInfo();
                info.UnpackedStreams.Add(folder.UnpackedStreams[0]);
            }

            bool loop = true;
            var prop = HeaderProperty.kEnd;
            while (loop)
            {
                prop = headerBytes.ReadProperty();
                switch (prop)
                {
                    case HeaderProperty.kNumUnPackStream:
                        {
                            info.UnpackedStreams.Clear();
                            foreach (var folder in info.Folders)
                            {
                                var numStreams = (int)headerBytes.ReadEncodedInt64();
                                folder.UnpackedStreams = new UnpackedStreamInfo[numStreams];
                                folder.UnpackedStreams.Initialize(() => new UnpackedStreamInfo());
                                info.UnpackedStreams.AddRange(folder.UnpackedStreams);
                            }
                        }
                        break;
                    case HeaderProperty.kCRC:
                    case HeaderProperty.kSize:
                    case HeaderProperty.kEnd:
                        {
                            loop = false;
                        }
                        break;
                    default:
                        throw new InvalidFormatException(prop.ToString());
                }
            }

            int si = 0;
            for (int i = 0; i < info.Folders.Length; i++)
            {
                var folder = info.Folders[i];
                ulong sum = 0;
                if (folder.UnpackedStreams.Length == 0)
                {
                    continue;
                }
                if (prop == HeaderProperty.kSize)
                {
                    for (int j = 1; j < folder.UnpackedStreams.Length; j++)
                    {
                        ulong size = headerBytes.ReadEncodedInt64();
                        info.UnpackedStreams[si].UnpackedSize = size;
                        sum += size;
                        si++;
                    }
                }
                info.UnpackedStreams[si].UnpackedSize = folder.GetUnpackSize() - sum;
                si++;
            }
            if (prop == HeaderProperty.kSize)
            {
                prop = headerBytes.ReadProperty();
            }

            int numDigests = 0;
            foreach (var folder in info.Folders)
            {
                if (folder.UnpackedStreams.Length != 1 || !folder.UnpackCRC.HasValue)
                {
                    numDigests += folder.UnpackedStreams.Length;
                }
            }

            si = 0;
            while (true)
            {
                if (prop == HeaderProperty.kCRC)
                {
                    int digestIndex = 0;
                    uint?[] digests2;
                    UnPackDigests(headerBytes, numDigests, out digests2);
                    for (uint i = 0; i < info.Folders.Length; i++)
                    {
                        Folder folder = info.Folders[i];
                        if (folder.UnpackedStreams.Length == 1 && folder.UnpackCRC.HasValue)
                        {
                            info.UnpackedStreams[si].Digest = folder.UnpackCRC;
                            si++;
                        }
                        else
                        {
                            for (uint j = 0; j < folder.UnpackedStreams.Length; j++, digestIndex++)
                            {
                                info.UnpackedStreams[si].Digest = digests2[digestIndex];
                                si++;
                            }
                        }
                    }
                }
                else if (prop == HeaderProperty.kEnd)
                    return;
                prop = headerBytes.ReadProperty();
            }
        }

        private void ReadUnPackInfo(StreamsInfo info, HeaderBuffer headerBytes)
        {
            var prop = headerBytes.ReadProperty();
            int count = (int)headerBytes.ReadEncodedInt64();
            info.Folders = new Folder[count];
            if (headerBytes.ReadByte() != 0)
            {
                throw new NotSupportedException("External flag");
            }

            for (int i = 0; i < count; i++)
            {
                info.Folders[i] = ReadFolder(headerBytes);
            }

            prop = headerBytes.ReadProperty();
            if (prop != HeaderProperty.kCodersUnPackSize)
            {
                throw new InvalidFormatException("Expected Size Property");
            }

            foreach (var folder in info.Folders)
            {
                int numOutStreams = folder.Coders.Aggregate(0, (sum, coder) => sum + (int)coder.NumberOfOutStreams);

                folder.UnpackedStreamSizes = new ulong[numOutStreams];

                for (uint j = 0; j < numOutStreams; j++)
                {
                    folder.UnpackedStreamSizes[j] = headerBytes.ReadEncodedInt64();
                }
            }


            prop = headerBytes.ReadProperty();
            if (prop != HeaderProperty.kCRC)
            {
                return;
            }
            uint?[] crcs;
            UnPackDigests(headerBytes, info.Folders.Length, out crcs);
            for (int i = 0; i < info.Folders.Length; i++)
            {
                Folder folder = info.Folders[i];
                folder.UnpackCRC = crcs[i];
            } prop = headerBytes.ReadProperty();
            if (prop != HeaderProperty.kEnd)
            {
                throw new InvalidFormatException("Expected End property");
            }
        }

        private static void UnPackDigests(HeaderBuffer headerBytes, int numItems, out uint?[] digests)
        {
            var digestsDefined = headerBytes.ReadBoolFlagsDefaultTrue(numItems);
            digests = new uint?[numItems];
            for (int i = 0; i < numItems; i++)
            {
                if (digestsDefined[i])
                {
                    digests[i] = headerBytes.ReadUInt32();
                }
            }
        }

        private Folder ReadFolder(HeaderBuffer headerBytes)
        {
            Folder folder = new Folder(this);
            folder.Coders = headerBytes.CreateArray<CodersInfo>();

            int numInStreams = 0;
            int numOutStreams = 0;

            foreach (var coder in folder.Coders)
            {
                byte mainByte = headerBytes.ReadByte();
                int size = (byte)(mainByte & 0xF);
                coder.Method = headerBytes.ReadBytes(size);
                if ((mainByte & 0x10) != 0)
                {
                    coder.NumberOfInStreams = headerBytes.ReadEncodedInt64();
                    coder.NumberOfOutStreams = headerBytes.ReadEncodedInt64();
                }
                else
                {
                    coder.NumberOfInStreams = 1;
                    coder.NumberOfOutStreams = 1;
                }
                if ((mainByte & 0x20) != 0)
                {
                    ulong propertiesSize = headerBytes.ReadEncodedInt64();
                    coder.Properties = headerBytes.ReadBytes((int)propertiesSize);
                }
                while ((mainByte & 0x80) != 0)
                {
                    mainByte = headerBytes.ReadByte();
                    headerBytes.ReadBytes(mainByte & 0xF);
                    if ((mainByte & 0x10) != 0)
                    {
                        headerBytes.ReadEncodedInt64();
                        headerBytes.ReadEncodedInt64();
                    }
                    if ((mainByte & 0x20) != 0)
                    {
                        ulong propertiesSize = headerBytes.ReadEncodedInt64();
                        headerBytes.ReadBytes((int)propertiesSize);
                    }
                }
                numInStreams += (int)coder.NumberOfInStreams;
                numOutStreams += (int)coder.NumberOfOutStreams;
            }

            int numBindPairs = numOutStreams - 1;
            folder.BindPairs = new BindPair[numBindPairs];

            for (int i = 0; i < numBindPairs; i++)
            {
                BindPair bindpair = new BindPair();
                folder.BindPairs[i] = bindpair;
                bindpair.InIndex = headerBytes.ReadEncodedInt64();
                bindpair.OutIndex = headerBytes.ReadEncodedInt64();
            }


            int numPackedStreams = numInStreams - numBindPairs;

            folder.PackedStreamIndices = new ulong[numPackedStreams];

            if (numPackedStreams == 1)
            {
                uint pi = 0;
                for (uint j = 0; j < numInStreams; j++)
                {
                    if (!folder.BindPairs.Where(x => x.InIndex == j).Any())
                    {
                        folder.PackedStreamIndices[pi++] = j;
                        break;
                    }
                }
            }
            else
            {
                for (uint i = 0; i < numPackedStreams; i++)
                {
                    folder.PackedStreamIndices[i] = headerBytes.ReadEncodedInt64();
                }
            }
            return folder;
        }

        private static void ReadPackInfo(StreamsInfo info, HeaderBuffer headerBytes)
        {
            info.PackPosition = headerBytes.ReadEncodedInt64();
            int count = (int)headerBytes.ReadEncodedInt64();

            info.PackedStreams = new PackedStreamInfo[count];
            for (int i = 0; i < count; i++)
            {
                info.PackedStreams[i] = new PackedStreamInfo();
            }
            var prop = headerBytes.ReadProperty();
            if (prop != HeaderProperty.kSize)
            {
                throw new InvalidFormatException("Expected Size Property");
            }
            for (int i = 0; i < count; i++)
            {
                info.PackedStreams[i].PackedSize = headerBytes.ReadEncodedInt64();

            }
            for (int i = 0; i < count; i++)
            {
                prop = headerBytes.ReadProperty();
                if (prop != HeaderProperty.kCRC)
                {
                    break;
                }
                info.PackedStreams[i].Crc = headerBytes.ReadEncodedInt64();
            }
        }

    }

}
