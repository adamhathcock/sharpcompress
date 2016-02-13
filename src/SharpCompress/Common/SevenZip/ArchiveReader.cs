using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Compressor.LZMA;
using SharpCompress.Compressor.LZMA.Utilites;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip
{
    internal class ArchiveReader
    {
        internal Stream _stream;
        internal Stack<DataReader> _readerStack = new Stack<DataReader>();
        internal DataReader _currentReader;
        internal long _streamOrigin;
        internal long _streamEnding;
        internal byte[] _header;

        private Dictionary<int, Stream> _cachedStreams = new Dictionary<int, Stream>();

        internal void AddByteStream(byte[] buffer, int offset, int length)
        {
            _readerStack.Push(_currentReader);
            _currentReader = new DataReader(buffer, offset, length);
        }

        internal void DeleteByteStream()
        {
            _currentReader = _readerStack.Pop();
        }

        #region Private Methods - Data Reader

        internal Byte ReadByte()
        {
            return _currentReader.ReadByte();
        }

        private void ReadBytes(byte[] buffer, int offset, int length)
        {
            _currentReader.ReadBytes(buffer, offset, length);
        }

        private ulong ReadNumber()
        {
            return _currentReader.ReadNumber();
        }

        internal int ReadNum()
        {
            return _currentReader.ReadNum();
        }

        private uint ReadUInt32()
        {
            return _currentReader.ReadUInt32();
        }

        private ulong ReadUInt64()
        {
            return _currentReader.ReadUInt64();
        }

        private BlockType? ReadId()
        {
            ulong id = _currentReader.ReadNumber();
            if (id > 25)
                return null;
#if DEBUG
            Log.WriteLine("ReadId: {0}", (BlockType)id);
#endif
            return (BlockType)id;
        }

        private void SkipData(long size)
        {
            _currentReader.SkipData(size);
        }

        private void SkipData()
        {
            _currentReader.SkipData();
        }

        private void WaitAttribute(BlockType attribute)
        {
            for (; ; )
            {
                BlockType? type = ReadId();
                if (type == attribute)
                    return;
                if (type == BlockType.End)
                    throw new InvalidOperationException();
                SkipData();
            }
        }

        private void ReadArchiveProperties()
        {
            while (ReadId() != BlockType.End)
                SkipData();
        }

        #endregion

        #region Private Methods - Reader Utilities

        private BitVector ReadBitVector(int length)
        {
            var bits = new BitVector(length);

            byte data = 0;
            byte mask = 0;

            for (int i = 0; i < length; i++)
            {
                if (mask == 0)
                {
                    data = ReadByte();
                    mask = 0x80;
                }

                if ((data & mask) != 0)
                    bits.SetBit(i);

                mask >>= 1;
            }

            return bits;
        }

        private BitVector ReadOptionalBitVector(int length)
        {
            byte allTrue = ReadByte();
            if (allTrue != 0)
                return new BitVector(length, true);

            return ReadBitVector(length);
        }

        private void ReadNumberVector(List<byte[]> dataVector, int numFiles, Action<int, long?> action)
        {
            var defined = ReadOptionalBitVector(numFiles);

            using (CStreamSwitch streamSwitch = new CStreamSwitch())
            {
                streamSwitch.Set(this, dataVector);

                for (int i = 0; i < numFiles; i++)
                {
                    if (defined[i])
                        action(i, checked((long)ReadUInt64()));
                    else
                        action(i, null);
                }
            }
        }

        private DateTime TranslateTime(long time)
        {
            // FILETIME = 100-nanosecond intervals since January 1, 1601 (UTC)
            return DateTime.FromFileTimeUtc(time).ToLocalTime();
        }

        private DateTime? TranslateTime(long? time)
        {
            if (time.HasValue)
                return TranslateTime(time.Value);
            else
                return null;
        }

        private void ReadDateTimeVector(List<byte[]> dataVector, int numFiles, Action<int, DateTime?> action)
        {
            ReadNumberVector(dataVector, numFiles, (index, value) => action(index, TranslateTime(value)));
        }

        private void ReadAttributeVector(List<byte[]> dataVector, int numFiles, Action<int, uint?> action)
        {
            BitVector boolVector = ReadOptionalBitVector(numFiles);
            using (var streamSwitch = new CStreamSwitch())
            {
                streamSwitch.Set(this, dataVector);
                for (int i = 0; i < numFiles; i++)
                {
                    if (boolVector[i])
                        action(i, ReadUInt32());
                    else
                        action(i, null);
                }
            }
        }

        #endregion

        #region Private Methods

        private void GetNextFolderItem(CFolder folder)
        {
#if DEBUG
            Log.WriteLine("-- GetNextFolderItem --");
            Log.PushIndent();
#endif
            try
            {
                int numCoders = ReadNum();
#if DEBUG
                Log.WriteLine("NumCoders: " + numCoders);
#endif
                folder.Coders = new List<CCoderInfo>(numCoders);
                int numInStreams = 0;
                int numOutStreams = 0;
                for (int i = 0; i < numCoders; i++)
                {
#if DEBUG
                    Log.WriteLine("-- Coder --");
                    Log.PushIndent();
#endif
                    try
                    {
                        CCoderInfo coder = new CCoderInfo();
                        folder.Coders.Add(coder);

                        byte mainByte = ReadByte();
                        int idSize = (mainByte & 0xF);
                        byte[] longID = new byte[idSize];
                        ReadBytes(longID, 0, idSize);
#if DEBUG
                        Log.WriteLine("MethodId: " + String.Join("", Enumerable.Range(0, idSize).Select(x => longID[x].ToString("x2")).ToArray()));
#endif
                        if (idSize > 8)
                            throw new NotSupportedException();
                        ulong id = 0;
                        for (int j = 0; j < idSize; j++)
                            id |= (ulong)longID[idSize - 1 - j] << (8 * j);
                        coder.MethodId = new CMethodId(id);

                        if ((mainByte & 0x10) != 0)
                        {
                            coder.NumInStreams = ReadNum();
                            coder.NumOutStreams = ReadNum();
#if DEBUG
                            Log.WriteLine("Complex Stream (In: " + coder.NumInStreams + " - Out: " + coder.NumOutStreams + ")");
#endif
                        }
                        else
                        {
#if DEBUG
                            Log.WriteLine("Simple Stream (In: 1 - Out: 1)");
#endif
                            coder.NumInStreams = 1;
                            coder.NumOutStreams = 1;
                        }

                        if ((mainByte & 0x20) != 0)
                        {
                            int propsSize = ReadNum();
                            coder.Props = new byte[propsSize];
                            ReadBytes(coder.Props, 0, propsSize);
#if DEBUG
                            Log.WriteLine("Settings: " + String.Join("", coder.Props.Select(bt => bt.ToString("x2")).ToArray()));
#endif
                        }

                        if ((mainByte & 0x80) != 0)
                            throw new NotSupportedException();

                        numInStreams += coder.NumInStreams;
                        numOutStreams += coder.NumOutStreams;
                    }
                    finally
                    {
#if DEBUG
                        Log.PopIndent();
#endif
                    }
                }

                int numBindPairs = numOutStreams - 1;
                folder.BindPairs = new List<CBindPair>(numBindPairs);
#if DEBUG
                Log.WriteLine("BindPairs: " + numBindPairs);
                Log.PushIndent();
#endif
                for (int i = 0; i < numBindPairs; i++)
                {
                    CBindPair bp = new CBindPair();
                    bp.InIndex = ReadNum();
                    bp.OutIndex = ReadNum();
                    folder.BindPairs.Add(bp);
#if DEBUG
                    Log.WriteLine("#" + i + " - In: " + bp.InIndex + " - Out: " + bp.OutIndex);
#endif
                }
#if DEBUG
                Log.PopIndent();
#endif

                if (numInStreams < numBindPairs)
                    throw new NotSupportedException();

                int numPackStreams = numInStreams - numBindPairs;
                //folder.PackStreams.Reserve(numPackStreams);
                if (numPackStreams == 1)
                {
                    for (int i = 0; i < numInStreams; i++)
                    {
                        if (folder.FindBindPairForInStream(i) < 0)
                        {
#if DEBUG
                            Log.WriteLine("Single PackStream: #" + i);
#endif
                            folder.PackStreams.Add(i);
                            break;
                        }
                    }

                    if (folder.PackStreams.Count != 1)
                        throw new NotSupportedException();
                }
                else
                {
#if DEBUG
                    Log.WriteLine("Multiple PackStreams ...");
                    Log.PushIndent();
#endif
                    for (int i = 0; i < numPackStreams; i++)
                    {
                        var num = ReadNum();
#if DEBUG
                        Log.WriteLine("#" + i + " - " + num);
#endif
                        folder.PackStreams.Add(num);
                    }
#if DEBUG
                    Log.PopIndent();
#endif
                }
            }
            finally
            {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private List<uint?> ReadHashDigests(int count)
        {
#if DEBUG
            Log.Write("ReadHashDigests:");
#endif

            var defined = ReadOptionalBitVector(count);
            var digests = new List<uint?>(count);
            for (int i = 0; i < count; i++)
            {
                if (defined[i])
                {
                    uint crc = ReadUInt32();
#if DEBUG
                    Log.Write("  " + crc.ToString("x8"));
#endif
                    digests.Add(crc);
                }
                else
                {
#if DEBUG
                    Log.Write("  ########");
#endif
                    digests.Add(null);
                }
            }
#if DEBUG

            Log.WriteLine();
#endif
            return digests;
        }

        private void ReadPackInfo(out long dataOffset, out List<long> packSizes, out List<uint?> packCRCs)
        {
#if DEBUG
            Log.WriteLine("-- ReadPackInfo --");
            Log.PushIndent();
#endif
            try
            {
                packCRCs = null;

                dataOffset = checked((long)ReadNumber());
#if DEBUG
                Log.WriteLine("DataOffset: " + dataOffset);
#endif

                int numPackStreams = ReadNum();
#if DEBUG
                Log.WriteLine("NumPackStreams: " + numPackStreams);
#endif

                WaitAttribute(BlockType.Size);
                packSizes = new List<long>(numPackStreams);
#if DEBUG
                Log.Write("Sizes:");
#endif
                for (int i = 0; i < numPackStreams; i++)
                {
                    var size = checked((long)ReadNumber());
#if DEBUG
                    Log.Write("  " + size);
#endif
                    packSizes.Add(size);
                }
#if DEBUG
                Log.WriteLine();
#endif

                BlockType? type;
                for (; ; )
                {
                    type = ReadId();
                    if (type == BlockType.End)
                        break;
                    if (type == BlockType.CRC)
                    {
                        packCRCs = ReadHashDigests(numPackStreams);
                        continue;
                    }
                    SkipData();
                }

                if (packCRCs == null)
                {
                    packCRCs = new List<uint?>(numPackStreams);
                    for (int i = 0; i < numPackStreams; i++)
                        packCRCs.Add(null);
                }
            }
            finally
            {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private void ReadUnpackInfo(List<byte[]> dataVector, out List<CFolder> folders)
        {
#if DEBUG
            Log.WriteLine("-- ReadUnpackInfo --");
            Log.PushIndent();
#endif
            try
            {
                WaitAttribute(BlockType.Folder);
                int numFolders = ReadNum();
#if DEBUG
                Log.WriteLine("NumFolders: {0}", numFolders);
#endif

                using (CStreamSwitch streamSwitch = new CStreamSwitch())
                {
                    streamSwitch.Set(this, dataVector);
                    //folders.Clear();
                    //folders.Reserve(numFolders);
                    folders = new List<CFolder>(numFolders);
                    int index = 0;
                    for (int i = 0; i < numFolders; i++)
                    {
                        var f = new CFolder { FirstPackStreamId = index };
                        folders.Add(f);
                        GetNextFolderItem(f);
                        index += f.PackStreams.Count;
                    }
                }

                WaitAttribute(BlockType.CodersUnpackSize);
#if DEBUG
                Log.WriteLine("UnpackSizes:");
#endif
                for (int i = 0; i < numFolders; i++)
                {
                    CFolder folder = folders[i];
#if DEBUG
                    Log.Write("  #" + i + ":");
#endif
                    int numOutStreams = folder.GetNumOutStreams();
                    for (int j = 0; j < numOutStreams; j++)
                    {
                        long size = checked((long)ReadNumber());
#if DEBUG
                        Log.Write("  " + size);
#endif
                        folder.UnpackSizes.Add(size);
                    }
#if DEBUG
                    Log.WriteLine();
#endif
                }

                for (; ; )
                {
                    BlockType? type = ReadId();
                    if (type == BlockType.End)
                        return;

                    if (type == BlockType.CRC)
                    {
                        List<uint?> crcs = ReadHashDigests(numFolders);
                        for (int i = 0; i < numFolders; i++)
                            folders[i].UnpackCRC = crcs[i];
                        continue;
                    }

                    SkipData();
                }
            }
            finally
            {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private void ReadSubStreamsInfo(List<CFolder> folders, out List<int> numUnpackStreamsInFolders,
                                        out List<long> unpackSizes, out List<uint?> digests)
        {
#if DEBUG
            Log.WriteLine("-- ReadSubStreamsInfo --");
            Log.PushIndent();
#endif
            try
            {
                numUnpackStreamsInFolders = null;

                BlockType? type;
                for (; ; )
                {
                    type = ReadId();
                    if (type == BlockType.NumUnpackStream)
                    {
                        numUnpackStreamsInFolders = new List<int>(folders.Count);
#if DEBUG
                        Log.Write("NumUnpackStreams:");
#endif
                        for (int i = 0; i < folders.Count; i++)
                        {
                            var num = ReadNum();
#if DEBUG
                            Log.Write("  " + num);
#endif
                            numUnpackStreamsInFolders.Add(num);
                        }
#if DEBUG
                        Log.WriteLine();
#endif
                        continue;
                    }
                    if (type == BlockType.CRC || type == BlockType.Size)
                        break;
                    if (type == BlockType.End)
                        break;
                    SkipData();
                }

                if (numUnpackStreamsInFolders == null)
                {
                    numUnpackStreamsInFolders = new List<int>(folders.Count);
                    for (int i = 0; i < folders.Count; i++)
                        numUnpackStreamsInFolders.Add(1);
                }

                unpackSizes = new List<long>(folders.Count);
                for (int i = 0; i < numUnpackStreamsInFolders.Count; i++)
                {
                    // v3.13 incorrectly worked with empty folders
                    // v4.07: we check that folder is empty
                    int numSubstreams = numUnpackStreamsInFolders[i];
                    if (numSubstreams == 0)
                        continue;
#if DEBUG
                    Log.Write("#{0} StreamSizes:", i);
#endif
                    long sum = 0;
                    for (int j = 1; j < numSubstreams; j++)
                    {
                        if (type == BlockType.Size)
                        {
                            long size = checked((long)ReadNumber());
#if DEBUG
                            Log.Write("  " + size);
#endif
                            unpackSizes.Add(size);
                            sum += size;
                        }
                    }
                    unpackSizes.Add(folders[i].GetUnpackSize() - sum);
#if DEBUG
                    Log.WriteLine("  -  rest: " + unpackSizes.Last());
#endif
                }
                if (type == BlockType.Size)
                    type = ReadId();

                int numDigests = 0;
                int numDigestsTotal = 0;
                for (int i = 0; i < folders.Count; i++)
                {
                    int numSubstreams = numUnpackStreamsInFolders[i];
                    if (numSubstreams != 1 || !folders[i].UnpackCRCDefined)
                        numDigests += numSubstreams;
                    numDigestsTotal += numSubstreams;
                }

                digests = null;

                for (; ; )
                {
                    if (type == BlockType.CRC)
                    {
                        digests = new List<uint?>(numDigestsTotal);

                        List<uint?> digests2 = ReadHashDigests(numDigests);

                        int digestIndex = 0;
                        for (int i = 0; i < folders.Count; i++)
                        {
                            int numSubstreams = numUnpackStreamsInFolders[i];
                            CFolder folder = folders[i];
                            if (numSubstreams == 1 && folder.UnpackCRCDefined)
                            {
                                digests.Add(folder.UnpackCRC.Value);
                            }
                            else
                            {
                                for (int j = 0; j < numSubstreams; j++, digestIndex++)
                                    digests.Add(digests2[digestIndex]);
                            }
                        }

                        if (digestIndex != numDigests || numDigestsTotal != digests.Count)
                            System.Diagnostics.Debugger.Break();
                    }
                    else if (type == BlockType.End)
                    {
                        if (digests == null)
                        {
                            digests = new List<uint?>(numDigestsTotal);
                            for (int i = 0; i < numDigestsTotal; i++)
                                digests.Add(null);
                        }
                        return;
                    }
                    else
                    {
                        SkipData();
                    }

                    type = ReadId();
                }
            }
            finally
            {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private void ReadStreamsInfo(
            List<byte[]> dataVector,
            out long dataOffset,
            out List<long> packSizes,
            out List<uint?> packCRCs,
            out List<CFolder> folders,
            out List<int> numUnpackStreamsInFolders,
            out List<long> unpackSizes,
            out List<uint?> digests)
        {
#if DEBUG
            Log.WriteLine("-- ReadStreamsInfo --");
            Log.PushIndent();
#endif
            try
            {
                dataOffset = long.MinValue;
                packSizes = null;
                packCRCs = null;
                folders = null;
                numUnpackStreamsInFolders = null;
                unpackSizes = null;
                digests = null;

                for (; ; )
                {
                    switch (ReadId())
                    {
                        case BlockType.End:
                            return;
                        case BlockType.PackInfo:
                            ReadPackInfo(out dataOffset, out packSizes, out packCRCs);
                            break;
                        case BlockType.UnpackInfo:
                            ReadUnpackInfo(dataVector, out folders);
                            break;
                        case BlockType.SubStreamsInfo:
                            ReadSubStreamsInfo(folders, out numUnpackStreamsInFolders, out unpackSizes, out digests);
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                }
            }
            finally
            {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private List<byte[]> ReadAndDecodePackedStreams(long baseOffset, IPasswordProvider pass)
        {
#if DEBUG
            Log.WriteLine("-- ReadAndDecodePackedStreams --");
            Log.PushIndent();
#endif
            try
            {
                long dataStartPos;
                List<long> packSizes;
                List<uint?> packCRCs;
                List<CFolder> folders;
                List<int> numUnpackStreamsInFolders;
                List<long> unpackSizes;
                List<uint?> digests;

                ReadStreamsInfo(null,
                                out dataStartPos,
                                out packSizes,
                                out packCRCs,
                                out folders,
                                out numUnpackStreamsInFolders,
                                out unpackSizes,
                                out digests);

                dataStartPos += baseOffset;

                var dataVector = new List<byte[]>(folders.Count);
                int packIndex = 0;
                foreach (var folder in folders)
                {
                    long oldDataStartPos = dataStartPos;
                    long[] myPackSizes = new long[folder.PackStreams.Count];
                    for (int i = 0; i < myPackSizes.Length; i++)
                    {
                        long packSize = packSizes[packIndex + i];
                        myPackSizes[i] = packSize;
                        dataStartPos += packSize;
                    }

                    var outStream = DecoderStreamHelper.CreateDecoderStream(_stream, oldDataStartPos, myPackSizes,
                                                                            folder, pass);

                    int unpackSize = checked((int)folder.GetUnpackSize());
                    byte[] data = new byte[unpackSize];
                    outStream.ReadExact(data, 0, data.Length);
                    if (outStream.ReadByte() >= 0)
                        throw new InvalidOperationException("Decoded stream is longer than expected.");
                    dataVector.Add(data);

                    if (folder.UnpackCRCDefined)
                        if (CRC.Finish(CRC.Update(CRC.kInitCRC, data, 0, unpackSize)) != folder.UnpackCRC)
                            throw new InvalidOperationException("Decoded stream does not match expected CRC.");
                }
                return dataVector;
            }
            finally
            {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private void ReadHeader(ArchiveDatabase db, IPasswordProvider getTextPassword)
        {
#if DEBUG
            Log.WriteLine("-- ReadHeader --");
            Log.PushIndent();
#endif
            try
            {
                BlockType? type = ReadId();

                if (type == BlockType.ArchiveProperties)
                {
                    ReadArchiveProperties();
                    type = ReadId();
                }

                List<byte[]> dataVector = null;
                if (type == BlockType.AdditionalStreamsInfo)
                {
                    dataVector = ReadAndDecodePackedStreams(db.StartPositionAfterHeader, getTextPassword);
                    type = ReadId();
                }

                List<long> unpackSizes;
                List<uint?> digests;

                if (type == BlockType.MainStreamsInfo)
                {
                    ReadStreamsInfo(dataVector,
                                    out db.DataStartPosition,
                                    out db.PackSizes,
                                    out db.PackCRCs,
                                    out db.Folders,
                                    out db.NumUnpackStreamsVector,
                                    out unpackSizes,
                                    out digests);

                    db.DataStartPosition += db.StartPositionAfterHeader;
                    type = ReadId();
                }
                else
                {
                    unpackSizes = new List<long>(db.Folders.Count);
                    digests = new List<uint?>(db.Folders.Count);
                    db.NumUnpackStreamsVector = new List<int>(db.Folders.Count);
                    for (int i = 0; i < db.Folders.Count; i++)
                    {
                        var folder = db.Folders[i];
                        unpackSizes.Add(folder.GetUnpackSize());
                        digests.Add(folder.UnpackCRC);
                        db.NumUnpackStreamsVector.Add(1);
                    }
                }

                db.Files.Clear();

                if (type == BlockType.End)
                    return;

                if (type != BlockType.FilesInfo)
                    throw new InvalidOperationException();

                int numFiles = ReadNum();
#if DEBUG
                Log.WriteLine("NumFiles: " + numFiles);
#endif
                db.Files = new List<CFileItem>(numFiles);
                for (int i = 0; i < numFiles; i++)
                    db.Files.Add(new CFileItem());

                BitVector emptyStreamVector = new BitVector(numFiles);
                BitVector emptyFileVector = null;
                BitVector antiFileVector = null;
                int numEmptyStreams = 0;

                for (; ; )
                {
                    type = ReadId();
                    if (type == BlockType.End)
                        break;

                    long size = checked((long)ReadNumber()); // TODO: throw invalid data on negative
                    int oldPos = _currentReader.Offset;
                    switch (type)
                    {
                        case BlockType.Name:
                            using (var streamSwitch = new CStreamSwitch())
                            {
                                streamSwitch.Set(this, dataVector);
#if DEBUG
                                Log.Write("FileNames:");
#endif
                                for (int i = 0; i < db.Files.Count; i++)
                                {
                                    db.Files[i].Name = _currentReader.ReadString();
#if DEBUG
                                    Log.Write("  " + db.Files[i].Name);
#endif
                                }
#if DEBUG
                                Log.WriteLine();
#endif
                            }
                            break;
                        case BlockType.WinAttributes:
#if DEBUG
                            Log.Write("WinAttributes:");
#endif
                            ReadAttributeVector(dataVector, numFiles, delegate(int i, uint? attr)
                                                                          {
                                                                              db.Files[i].Attrib = attr;
#if DEBUG
                                                                              Log.Write("  " + (attr.HasValue ? attr.Value.ToString("x8") : "n/a"));
#endif
                                                                          });
#if DEBUG
                            Log.WriteLine();
#endif
                            break;
                        case BlockType.EmptyStream:
                            emptyStreamVector = ReadBitVector(numFiles);
#if DEBUG

                            Log.Write("EmptyStream: ");
#endif
                            for (int i = 0; i < emptyStreamVector.Length; i++)
                            {
                                if (emptyStreamVector[i])
                                {
#if DEBUG
                                    Log.Write("x");
#endif
                                    numEmptyStreams++;
                                }
                                else
                                {
#if DEBUG
                                    Log.Write(".");
#endif
                                }
                            }
#if DEBUG
                            Log.WriteLine();
#endif

                            emptyFileVector = new BitVector(numEmptyStreams);
                            antiFileVector = new BitVector(numEmptyStreams);
                            break;
                        case BlockType.EmptyFile:
                            emptyFileVector = ReadBitVector(numEmptyStreams);
#if DEBUG
                            Log.Write("EmptyFile: ");
                            for (int i = 0; i < numEmptyStreams; i++)
                                Log.Write(emptyFileVector[i] ? "x" : ".");
                            Log.WriteLine();
#endif
                            break;
                        case BlockType.Anti:
                            antiFileVector = ReadBitVector(numEmptyStreams);
#if DEBUG
                            Log.Write("Anti: ");
                            for (int i = 0; i < numEmptyStreams; i++)
                                Log.Write(antiFileVector[i] ? "x" : ".");
                            Log.WriteLine();
#endif
                            break;
                        case BlockType.StartPos:
#if DEBUG
                            Log.Write("StartPos:");
#endif
                            ReadNumberVector(dataVector, numFiles, delegate(int i, long? startPos)
                                                                       {
                                                                           db.Files[i].StartPos = startPos;
#if DEBUG
                                                                           Log.Write("  " + (startPos.HasValue ? startPos.Value.ToString() : "n/a"));
#endif
                                                                       });
#if DEBUG
                            Log.WriteLine();
#endif
                            break;
                        case BlockType.CTime:
#if DEBUG
                            Log.Write("CTime:");
#endif
                            ReadDateTimeVector(dataVector, numFiles, delegate(int i, DateTime? time)
                                                                         {
                                                                             db.Files[i].CTime = time;
#if DEBUG
                                                                             Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
#endif
                                                                         });
#if DEBUG
                            Log.WriteLine();
#endif
                            break;
                        case BlockType.ATime:
#if DEBUG
                            Log.Write("ATime:");
#endif
                            ReadDateTimeVector(dataVector, numFiles, delegate(int i, DateTime? time)
                                                                         {
                                                                             db.Files[i].ATime = time;
#if DEBUG
                                                                             Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
#endif
                                                                         });
#if DEBUG
                            Log.WriteLine();
#endif
                            break;
                        case BlockType.MTime:
#if DEBUG
                            Log.Write("MTime:");
#endif
                            ReadDateTimeVector(dataVector, numFiles, delegate(int i, DateTime? time)
                                                                         {
                                                                             db.Files[i].MTime = time;
#if DEBUG
                                                                             Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
#endif
                                                                         });
#if DEBUG
                            Log.WriteLine();
#endif
                            break;
                        case BlockType.Dummy:
#if DEBUG
                            Log.Write("Dummy: " + size);
#endif
                            for (long j = 0; j < size; j++)
                                if (ReadByte() != 0)
                                    throw new InvalidOperationException();
                            break;
                        default:
                            SkipData(size);
                            break;
                    }

                    // since 0.3 record sizes must be correct
                    bool checkRecordsSize = (db.MajorVersion > 0 || db.MinorVersion > 2);
                    if (checkRecordsSize && _currentReader.Offset - oldPos != size)
                        throw new InvalidOperationException();
                }

                int emptyFileIndex = 0;
                int sizeIndex = 0;
                for (int i = 0; i < numFiles; i++)
                {
                    CFileItem file = db.Files[i];
                    file.HasStream = !emptyStreamVector[i];
                    if (file.HasStream)
                    {
                        file.IsDir = false;
                        file.IsAnti = false;
                        file.Size = unpackSizes[sizeIndex];
                        file.Crc = digests[sizeIndex];
                        sizeIndex++;
                    }
                    else
                    {
                        file.IsDir = !emptyFileVector[emptyFileIndex];
                        file.IsAnti = antiFileVector[emptyFileIndex];
                        emptyFileIndex++;
                        file.Size = 0;
                        file.Crc = null;
                    }
                }
            }
            finally
            {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        #endregion

        #region Public Methods

        public void Open(Stream stream)
        {
            Close();

            _streamOrigin = stream.Position;
            _streamEnding = stream.Length;

            // TODO: Check Signature!
            _header = new byte[0x20];
            for (int offset = 0; offset < 0x20; )
            {
                int delta = stream.Read(_header, offset, 0x20 - offset);
                if (delta == 0)
                    throw new EndOfStreamException();
                offset += delta;
            }

            _stream = stream;
        }

        public void Close()
        {
            if (_stream != null)
                _stream.Dispose();

            foreach (var stream in _cachedStreams.Values)
                stream.Dispose();

            _cachedStreams.Clear();
        }

        public ArchiveDatabase ReadDatabase(IPasswordProvider pass)
        {
            var db = new ArchiveDatabase();
            db.Clear();

            db.MajorVersion = _header[6];
            db.MinorVersion = _header[7];

            if (db.MajorVersion != 0)
                throw new InvalidOperationException();

            uint crcFromArchive = DataReader.Get32(_header, 8);
            long nextHeaderOffset = (long)DataReader.Get64(_header, 0xC);
            long nextHeaderSize = (long)DataReader.Get64(_header, 0x14);
            uint nextHeaderCrc = DataReader.Get32(_header, 0x1C);

            uint crc = CRC.kInitCRC;
            crc = CRC.Update(crc, nextHeaderOffset);
            crc = CRC.Update(crc, nextHeaderSize);
            crc = CRC.Update(crc, nextHeaderCrc);
            crc = CRC.Finish(crc);

            if (crc != crcFromArchive)
                throw new InvalidOperationException();

            db.StartPositionAfterHeader = _streamOrigin + 0x20;

            // empty header is ok
            if (nextHeaderSize == 0)
            {
                db.Fill();
                return db;
            }


            if (nextHeaderOffset < 0 || nextHeaderSize < 0 || nextHeaderSize > Int32.MaxValue)
                throw new InvalidOperationException();

            if (nextHeaderOffset > _streamEnding - db.StartPositionAfterHeader)
                throw new IndexOutOfRangeException();

            _stream.Seek(nextHeaderOffset, SeekOrigin.Current);

            byte[] header = new byte[nextHeaderSize];
            _stream.ReadExact(header, 0, header.Length);

            if (CRC.Finish(CRC.Update(CRC.kInitCRC, header, 0, header.Length)) != nextHeaderCrc)
                throw new InvalidOperationException();

            using (CStreamSwitch streamSwitch = new CStreamSwitch())
            {
                streamSwitch.Set(this, header);

                BlockType? type = ReadId();
                if (type != BlockType.Header)
                {
                    if (type != BlockType.EncodedHeader)
                        throw new InvalidOperationException();

                    var dataVector = ReadAndDecodePackedStreams(db.StartPositionAfterHeader, pass);

                    // compressed header without content is odd but ok
                    if (dataVector.Count == 0)
                    {
                        db.Fill();
                        return db;
                    }

                    if (dataVector.Count != 1)
                        throw new InvalidOperationException();

                    streamSwitch.Set(this, dataVector[0]);

                    if (ReadId() != BlockType.Header)
                        throw new InvalidOperationException();
                }

                ReadHeader(db, pass);
            }
            db.Fill();
            return db;
        }

        internal class CExtractFolderInfo
        {
            internal int FileIndex;
            internal int FolderIndex;
            internal List<bool> ExtractStatuses = new List<bool>();

            internal CExtractFolderInfo(int fileIndex, int folderIndex)
            {
                FileIndex = fileIndex;
                FolderIndex = folderIndex;
                if (fileIndex != -1)
                    ExtractStatuses.Add(true);
            }
        }

        private class FolderUnpackStream : Stream
        {
            private ArchiveDatabase _db;
            private int _startIndex;
            private List<bool> _extractStatuses;

            public FolderUnpackStream(ArchiveDatabase db, int p, int startIndex, List<bool> list)
            {
                this._db = db;
                this._startIndex = startIndex;
                this._extractStatuses = list;
            }

            #region Stream

            public override bool CanRead
            {
                get { return true; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override long Length
            {
                get { throw new NotSupportedException(); }
            }

            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            private Stream _stream;
            private long _rem;
            private int _currentIndex;

            private void ProcessEmptyFiles()
            {
                while (_currentIndex < _extractStatuses.Count && _db.Files[_startIndex + _currentIndex].Size == 0)
                {
                    OpenFile();
                    _stream.Dispose();
                    _stream = null;
                    _currentIndex++;
                }
            }

            private void OpenFile()
            {
                int index = _startIndex + _currentIndex;
#if DEBUG
                Log.WriteLine(_db.Files[index].Name);
#endif
                if (_db.Files[index].CrcDefined)
                    _stream = new CrcCheckStream(_db.Files[index].Crc.Value);
                else
                    _stream = new MemoryStream();
                _rem = _db.Files[index].Size;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                while (count != 0)
                {
                    if (_stream != null)
                    {
                        int write = count;
                        if (write > _rem)
                            write = (int)_rem;
                        _stream.Write(buffer, offset, write);
                        count -= write;
                        _rem -= write;
                        offset += write;
                        if (_rem == 0)
                        {
                            _stream.Dispose();
                            _stream = null;
                            _currentIndex++;
                            ProcessEmptyFiles();
                        }
                    }
                    else
                    {
                        ProcessEmptyFiles();
                        if (_currentIndex == _extractStatuses.Count)
                        {
                            // we support partial extracting
                            System.Diagnostics.Debugger.Break();
                            throw new NotSupportedException();
                        }
                        OpenFile();
                    }
                }
            }

            #endregion
        }

        private Stream GetCachedDecoderStream(ArchiveDatabase _db, int folderIndex, IPasswordProvider pw)
        {
            Stream s;
            if (!_cachedStreams.TryGetValue(folderIndex, out s))
            {
                CFolder folderInfo = _db.Folders[folderIndex];
                int packStreamIndex = _db.Folders[folderIndex].FirstPackStreamId;
                long folderStartPackPos = _db.GetFolderStreamPos(folderInfo, 0);
                List<long> packSizes = new List<long>();
                for (int j = 0; j < folderInfo.PackStreams.Count; j++)
                    packSizes.Add(_db.PackSizes[packStreamIndex + j]);

                s = DecoderStreamHelper.CreateDecoderStream(_stream, folderStartPackPos, packSizes.ToArray(), folderInfo,
                                                            pw);
                _cachedStreams.Add(folderIndex, s);
            }
            return s;
        }

        public Stream OpenStream(ArchiveDatabase _db, int fileIndex, IPasswordProvider pw)
        {
            int folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
            int numFilesInFolder = _db.NumUnpackStreamsVector[folderIndex];
            int firstFileIndex = _db.FolderStartFileIndex[folderIndex];
            if (firstFileIndex > fileIndex || fileIndex - firstFileIndex >= numFilesInFolder)
                throw new InvalidOperationException();

            int skipCount = fileIndex - firstFileIndex;
            long skipSize = 0;
            for (int i = 0; i < skipCount; i++)
                skipSize += _db.Files[firstFileIndex + i].Size;

            Stream s = GetCachedDecoderStream(_db, folderIndex, pw);
            s.Position = skipSize;
            return new ReadOnlySubStream(s, _db.Files[fileIndex].Size);
        }

        public void Extract(ArchiveDatabase _db, int[] indices, IPasswordProvider pw)
        {
            int numItems;
            bool allFilesMode = (indices == null);
            if (allFilesMode)
                numItems = _db.Files.Count;
            else
                numItems = indices.Length;

            if (numItems == 0)
                return;

            List<CExtractFolderInfo> extractFolderInfoVector = new List<CExtractFolderInfo>();
            for (int i = 0; i < numItems; i++)
            {
                int fileIndex = allFilesMode ? i : indices[i];

                int folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
                if (folderIndex == -1)
                {
                    extractFolderInfoVector.Add(new CExtractFolderInfo(fileIndex, -1));
                    continue;
                }

                if (extractFolderInfoVector.Count == 0 || folderIndex != extractFolderInfoVector.Last().FolderIndex)
                    extractFolderInfoVector.Add(new CExtractFolderInfo(-1, folderIndex));

                CExtractFolderInfo efi = extractFolderInfoVector.Last();

                int startIndex = _db.FolderStartFileIndex[folderIndex];
                for (int index = efi.ExtractStatuses.Count; index <= fileIndex - startIndex; index++)
                    efi.ExtractStatuses.Add(index == fileIndex - startIndex);
            }

            foreach (CExtractFolderInfo efi in extractFolderInfoVector)
            {
                int startIndex;
                if (efi.FileIndex != -1)
                    startIndex = efi.FileIndex;
                else
                    startIndex = _db.FolderStartFileIndex[efi.FolderIndex];

                var outStream = new FolderUnpackStream(_db, 0, startIndex, efi.ExtractStatuses);

                if (efi.FileIndex != -1)
                    continue;

                int folderIndex = efi.FolderIndex;
                CFolder folderInfo = _db.Folders[folderIndex];

                int packStreamIndex = _db.Folders[folderIndex].FirstPackStreamId;
                long folderStartPackPos = _db.GetFolderStreamPos(folderInfo, 0);

                List<long> packSizes = new List<long>();
                for (int j = 0; j < folderInfo.PackStreams.Count; j++)
                    packSizes.Add(_db.PackSizes[packStreamIndex + j]);

                // TODO: If the decoding fails the last file may be extracted incompletely. Delete it?

                Stream s = DecoderStreamHelper.CreateDecoderStream(_stream, folderStartPackPos, packSizes.ToArray(),
                                                                   folderInfo, pw);
                byte[] buffer = new byte[4 << 10];
                for (; ; )
                {
                    int processed = s.Read(buffer, 0, buffer.Length);
                    if (processed == 0)
                        break;
                    outStream.Write(buffer, 0, processed);
                }
            }
        }

        public IEnumerable<CFileItem> GetFiles(ArchiveDatabase db)
        {
            return db.Files;
        }

        public int GetFileIndex(ArchiveDatabase db, CFileItem item)
        {
            return db.Files.IndexOf(item);
        }

        #endregion
    }
}