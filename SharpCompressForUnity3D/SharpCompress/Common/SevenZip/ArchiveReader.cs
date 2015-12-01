namespace SharpCompress.Common.SevenZip
{
    using SharpCompress.Compressor.LZMA;
    using SharpCompress.Compressor.LZMA.Utilites;
    using SharpCompress.IO;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
   

    internal class ArchiveReader
    {
        private Dictionary<int, Stream> _cachedStreams = new Dictionary<int, Stream>();
        internal DataReader _currentReader;
        internal byte[] _header;
        internal Stack<DataReader> _readerStack = new Stack<DataReader>();
        internal Stream _stream;
        internal long _streamEnding;
        internal long _streamOrigin;

        internal void AddByteStream(byte[] buffer, int offset, int length)
        {
            this._readerStack.Push(this._currentReader);
            this._currentReader = new DataReader(buffer, offset, length);
        }

        public void Close()
        {
            if (this._stream != null)
            {
                this._stream.Dispose();
            }
            foreach (Stream stream in this._cachedStreams.Values)
            {
                stream.Dispose();
            }
            this._cachedStreams.Clear();
        }

        internal void DeleteByteStream()
        {
            this._currentReader = this._readerStack.Pop();
        }

        public void Extract(ArchiveDatabase _db, int[] indices, IPasswordProvider pw)
        {
            int count;
            bool flag = indices == null;
            if (flag)
            {
                count = _db.Files.Count;
            }
            else
            {
                count = indices.Length;
            }
            if (count != 0)
            {
                int folderIndex;
                int num5;
                List<CExtractFolderInfo> source = new List<CExtractFolderInfo>();
                for (int i = 0; i < count; i++)
                {
                    int fileIndex = flag ? i : indices[i];
                    folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
                    if (folderIndex == -1)
                    {
                        source.Add(new CExtractFolderInfo(fileIndex, -1));
                    }
                    else
                    {
                        if ((source.Count == 0) || (folderIndex != Enumerable.Last<CExtractFolderInfo>(source).FolderIndex))
                        {
                            source.Add(new CExtractFolderInfo(-1, folderIndex));
                        }
                        CExtractFolderInfo info = Enumerable.Last<CExtractFolderInfo>(source);
                        num5 = _db.FolderStartFileIndex[folderIndex];
                        for (int j = info.ExtractStatuses.Count; j <= (fileIndex - num5); j++)
                        {
                            info.ExtractStatuses.Add(j == (fileIndex - num5));
                        }
                    }
                }
                foreach (CExtractFolderInfo info in source)
                {
                    int num10;
                    bool flag2;
                    if (info.FileIndex != -1)
                    {
                        num5 = info.FileIndex;
                    }
                    else
                    {
                        num5 = _db.FolderStartFileIndex[info.FolderIndex];
                    }
                    FolderUnpackStream stream = new FolderUnpackStream(_db, 0, num5, info.ExtractStatuses);
                    if (info.FileIndex != -1)
                    {
                        continue;
                    }
                    folderIndex = info.FolderIndex;
                    CFolder folder = _db.Folders[folderIndex];
                    int firstPackStreamId = _db.Folders[folderIndex].FirstPackStreamId;
                    long folderStreamPos = _db.GetFolderStreamPos(folder, 0);
                    List<long> list2 = new List<long>();
                    for (int k = 0; k < folder.PackStreams.Count; k++)
                    {
                        list2.Add(_db.PackSizes[firstPackStreamId + k]);
                    }
                    Stream stream2 = DecoderStreamHelper.CreateDecoderStream(this._stream, folderStreamPos, list2.ToArray(), folder, pw);
                    byte[] buffer = new byte[0x1000];
                    goto Label_0252;
                Label_0223:
                    num10 = stream2.Read(buffer, 0, buffer.Length);
                    if (num10 == 0)
                    {
                        continue;
                    }
                    stream.Write(buffer, 0, num10);
                Label_0252:
                    flag2 = true;
                    goto Label_0223;
                }
            }
        }

        private Stream GetCachedDecoderStream(ArchiveDatabase _db, int folderIndex, IPasswordProvider pw)
        {
            Stream stream;
            if (!this._cachedStreams.TryGetValue(folderIndex, out stream))
            {
                CFolder folder = _db.Folders[folderIndex];
                int firstPackStreamId = _db.Folders[folderIndex].FirstPackStreamId;
                long folderStreamPos = _db.GetFolderStreamPos(folder, 0);
                List<long> list = new List<long>();
                for (int i = 0; i < folder.PackStreams.Count; i++)
                {
                    list.Add(_db.PackSizes[firstPackStreamId + i]);
                }
                stream = DecoderStreamHelper.CreateDecoderStream(this._stream, folderStreamPos, list.ToArray(), folder, pw);
                this._cachedStreams.Add(folderIndex, stream);
            }
            return stream;
        }

        public int GetFileIndex(ArchiveDatabase db, CFileItem item)
        {
            return db.Files.IndexOf(item);
        }

        public IEnumerable<CFileItem> GetFiles(ArchiveDatabase db)
        {
            return db.Files;
        }

        private void GetNextFolderItem(CFolder folder)
        {
            SharpCompress.Compressor.LZMA.Log.WriteLine("-- GetNextFolderItem --");
            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
            try
            {
                int num4;
                int capacity = this.ReadNum();
                SharpCompress.Compressor.LZMA.Log.WriteLine("NumCoders: " + capacity);
                folder.Coders = new List<CCoderInfo>(capacity);
                int num2 = 0;
                int num3 = 0;
                for (num4 = 0; num4 < capacity; num4++)
                {
                    SharpCompress.Compressor.LZMA.Log.WriteLine("-- Coder --");
                    SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
                    try
                    {
                        CCoderInfo item = new CCoderInfo();
                        folder.Coders.Add(item);
                        byte num5 = this.ReadByte();
                        int length = num5 & 15;
                        byte[] longID = new byte[length];
                        this.ReadBytes(longID, 0, length);
                        SharpCompress.Compressor.LZMA.Log.WriteLine("MethodId: " + string.Join("", Enumerable.ToArray<string>(Enumerable.Select<int, string>(Enumerable.Range(0, length), delegate (int x) {
                            return longID[x].ToString("x2");
                        }))));
                        if (length > 8)
                        {
                            throw new NotSupportedException();
                        }
                        ulong id = 0L;
                        for (int i = 0; i < length; i++)
                        {
                            id |= (ulong)longID[(length - 1) - i] << (8 * i);
                        }
                        item.MethodId = new CMethodId(id);
                        if ((num5 & 0x10) != 0)
                        {
                            item.NumInStreams = this.ReadNum();
                            item.NumOutStreams = this.ReadNum();
                            SharpCompress.Compressor.LZMA.Log.WriteLine(string.Concat(new object[] { "Complex Stream (In: ", item.NumInStreams, " - Out: ", item.NumOutStreams, ")" }));
                        }
                        else
                        {
                            SharpCompress.Compressor.LZMA.Log.WriteLine("Simple Stream (In: 1 - Out: 1)");
                            item.NumInStreams = 1;
                            item.NumOutStreams = 1;
                        }
                        if ((num5 & 0x20) != 0)
                        {
                            int num9 = this.ReadNum();
                            item.Props = new byte[num9];
                            this.ReadBytes(item.Props, 0, num9);
                            SharpCompress.Compressor.LZMA.Log.WriteLine("Settings: " + string.Join("", Enumerable.ToArray<string>(Enumerable.Select<byte, string>(item.Props, delegate (byte bt) {
                                return bt.ToString("x2");
                            }))));
                        }
                        if ((num5 & 0x80) != 0)
                        {
                            throw new NotSupportedException();
                        }
                        num2 += item.NumInStreams;
                        num3 += item.NumOutStreams;
                    }
                    finally
                    {
                        SharpCompress.Compressor.LZMA.Log.PopIndent();
                    }
                }
                int num10 = num3 - 1;
                folder.BindPairs = new List<CBindPair>(num10);
                SharpCompress.Compressor.LZMA.Log.WriteLine("BindPairs: " + num10);
                SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
                for (num4 = 0; num4 < num10; num4++)
                {
                    CBindPair pair = new CBindPair();
                    pair.InIndex = this.ReadNum();
                    pair.OutIndex = this.ReadNum();
                    folder.BindPairs.Add(pair);
                    SharpCompress.Compressor.LZMA.Log.WriteLine(string.Concat(new object[] { "#", num4, " - In: ", pair.InIndex, " - Out: ", pair.OutIndex }));
                }
                SharpCompress.Compressor.LZMA.Log.PopIndent();
                if (num2 < num10)
                {
                    throw new NotSupportedException();
                }
                int num11 = num2 - num10;
                if (num11 == 1)
                {
                    for (num4 = 0; num4 < num2; num4++)
                    {
                        if (folder.FindBindPairForInStream(num4) < 0)
                        {
                            SharpCompress.Compressor.LZMA.Log.WriteLine("Single PackStream: #" + num4);
                            folder.PackStreams.Add(num4);
                            break;
                        }
                    }
                    if (folder.PackStreams.Count != 1)
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    SharpCompress.Compressor.LZMA.Log.WriteLine("Multiple PackStreams ...");
                    SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
                    for (num4 = 0; num4 < num11; num4++)
                    {
                        int num12 = this.ReadNum();
                        SharpCompress.Compressor.LZMA.Log.WriteLine(string.Concat(new object[] { "#", num4, " - ", num12 }));
                        folder.PackStreams.Add(num12);
                    }
                    SharpCompress.Compressor.LZMA.Log.PopIndent();
                }
            }
            finally
            {
                SharpCompress.Compressor.LZMA.Log.PopIndent();
            }
        }

        public void Open(Stream stream)
        {
            int num2;
            this.Close();
            this._streamOrigin = stream.Position;
            this._streamEnding = stream.Length;
            this._header = new byte[0x20];
            for (int i = 0; i < 0x20; i += num2)
            {
                num2 = stream.Read(this._header, i, 0x20 - i);
                if (num2 == 0)
                {
                    throw new EndOfStreamException();
                }
            }
            this._stream = stream;
        }

        public Stream OpenStream(ArchiveDatabase _db, int fileIndex, IPasswordProvider pw)
        {
            int folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
            int num2 = _db.NumUnpackStreamsVector[folderIndex];
            int num3 = _db.FolderStartFileIndex[folderIndex];
            if ((num3 > fileIndex) || ((fileIndex - num3) >= num2))
            {
                throw new InvalidOperationException();
            }
            int num4 = fileIndex - num3;
            long num5 = 0L;
            for (int i = 0; i < num4; i++)
            {
                num5 += _db.Files[num3 + i].Size;
            }
            Stream stream = this.GetCachedDecoderStream(_db, folderIndex, pw);
            stream.Position = num5;
            return new ReadOnlySubStream(stream, _db.Files[fileIndex].Size);
        }

        private List<byte[]> ReadAndDecodePackedStreams(long baseOffset, IPasswordProvider pass)
        {
            List<byte[]> list8;
            SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadAndDecodePackedStreams --");
            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
            try
            {
                long num;
                List<long> list;
                List<uint?> list2;
                List<CFolder> list3;
                List<int> list4;
                List<long> list5;
                List<uint?> list6;
                this.ReadStreamsInfo(null, out num, out list, out list2, out list3, out list4, out list5, out list6);
                num += baseOffset;
                List<byte[]> list7 = new List<byte[]>(list3.Count);
                int num2 = 0;
                foreach (CFolder folder in list3)
                {
                    long startPos = num;
                    long[] packSizes = new long[folder.PackStreams.Count];
                    for (int i = 0; i < packSizes.Length; i++)
                    {
                        long num5 = list[num2 + i];
                        packSizes[i] = num5;
                        num += num5;
                    }
                    Stream stream = DecoderStreamHelper.CreateDecoderStream(this._stream, startPos, packSizes, folder, pass);
                    int unpackSize = (int) folder.GetUnpackSize();
                    byte[] buffer = new byte[unpackSize];
                    Utils.ReadExact(stream, buffer, 0, buffer.Length);
                    if (stream.ReadByte() >= 0)
                    {
                        throw new InvalidOperationException("Decoded stream is longer than expected.");
                    }
                    list7.Add(buffer);
                    if (folder.UnpackCRCDefined)
                    {
                        uint num7 = CRC.Finish(CRC.Update(uint.MaxValue, buffer, 0, unpackSize));
                        if (num7 != folder.UnpackCRC)
                        {
                            throw new InvalidOperationException("Decoded stream does not match expected CRC.");
                        }
                    }
                }
                list8 = list7;
            }
            finally
            {
                SharpCompress.Compressor.LZMA.Log.PopIndent();
            }
            return list8;
        }

        private void ReadArchiveProperties()
        {
            while (true)
            {
                BlockType? nullable = this.ReadId();
                if ((((BlockType) nullable.GetValueOrDefault()) == BlockType.End) && nullable.HasValue)
                {
                    return;
                }
                this.SkipData();
            }
        }

        private void ReadAttributeVector(List<byte[]> dataVector, int numFiles, Action<int, uint?> action)
        {
            BitVector vector = this.ReadOptionalBitVector(numFiles);
            using (CStreamSwitch switch2 = new CStreamSwitch())
            {
                switch2.Set(this, dataVector);
                for (int i = 0; i < numFiles; i++)
                {
                    if (vector[i])
                    {
                        action(i, new uint?(this.ReadUInt32()));
                    }
                    else
                    {
                        uint? nullable = null;
                        action(i, nullable);
                    }
                }
            }
        }

        private BitVector ReadBitVector(int length)
        {
            BitVector vector = new BitVector(length);
            byte num = 0;
            byte num2 = 0;
            for (int i = 0; i < length; i++)
            {
                if (num2 == 0)
                {
                    num = this.ReadByte();
                    num2 = 0x80;
                }
                if ((num & num2) != 0)
                {
                    vector.SetBit(i);
                }
                num2 = (byte) (num2 >> 1);
            }
            return vector;
        }

        internal byte ReadByte()
        {
            return this._currentReader.ReadByte();
        }

        private void ReadBytes(byte[] buffer, int offset, int length)
        {
            this._currentReader.ReadBytes(buffer, offset, length);
        }

        public ArchiveDatabase ReadDatabase(IPasswordProvider pass)
        {
            ArchiveDatabase db = new ArchiveDatabase();
            db.Clear();
            db.MajorVersion = this._header[6];
            db.MinorVersion = this._header[7];
            if (db.MajorVersion != 0)
            {
                throw new InvalidOperationException();
            }
            uint num = DataReader.Get32(this._header, 8);
            long num2 = (long) DataReader.Get64(this._header, 12);
            long num3 = (long) DataReader.Get64(this._header, 20);
            uint num4 = DataReader.Get32(this._header, 0x1c);
            uint maxValue = uint.MaxValue;
            if (CRC.Finish(CRC.Update(CRC.Update(CRC.Update(maxValue, num2), num3), num4)) != num)
            {
                throw new InvalidOperationException();
            }
            db.StartPositionAfterHeader = this._streamOrigin + 0x20L;
            if (num3 == 0L)
            {
                db.Fill();
                return db;
            }
            if (((num2 < 0L) || (num3 < 0L)) || (num3 > 0x7fffffffL))
            {
                throw new InvalidOperationException();
            }
            if (num2 > (this._streamEnding - db.StartPositionAfterHeader))
            {
                throw new IndexOutOfRangeException();
            }
            this._stream.Seek(num2, SeekOrigin.Current);
            byte[] buffer = new byte[num3];
            Utils.ReadExact(this._stream, buffer, 0, buffer.Length);
            if (CRC.Finish(CRC.Update(uint.MaxValue, buffer, 0, buffer.Length)) != num4)
            {
                throw new InvalidOperationException();
            }
            using (CStreamSwitch switch2 = new CStreamSwitch())
            {
                switch2.Set(this, buffer);
                BlockType? nullable = this.ReadId();
                if (((BlockType) nullable) != BlockType.Header)
                {
                    if (((BlockType) nullable) != BlockType.EncodedHeader)
                    {
                        throw new InvalidOperationException();
                    }
                    List<byte[]> list = this.ReadAndDecodePackedStreams(db.StartPositionAfterHeader, pass);
                    if (list.Count == 0)
                    {
                        db.Fill();
                        return db;
                    }
                    if (list.Count != 1)
                    {
                        throw new InvalidOperationException();
                    }
                    switch2.Set(this, list[0]);
                    if (((BlockType) this.ReadId()) != BlockType.Header)
                    {
                        throw new InvalidOperationException();
                    }
                }
                this.ReadHeader(db, pass);
            }
            db.Fill();
            return db;
        }

        private void ReadDateTimeVector(List<byte[]> dataVector, int numFiles, Action<int, DateTime?> action)
        {
            this.ReadNumberVector(dataVector, numFiles, delegate (int index, long? value) {
                action(index, this.TranslateTime(value));
            });
        }

        private List<uint?> ReadHashDigests(int count)
        {
            SharpCompress.Compressor.LZMA.Log.Write("ReadHashDigests:");
            BitVector vector = this.ReadOptionalBitVector(count);
            List<uint?> list = new List<uint?>(count);
            for (int i = 0; i < count; i++)
            {
                if (vector[i])
                {
                    uint num2 = this.ReadUInt32();
                    SharpCompress.Compressor.LZMA.Log.Write("  " + num2.ToString("x8"));
                    list.Add(new uint?(num2));
                }
                else
                {
                    SharpCompress.Compressor.LZMA.Log.Write("  ########");
                    uint? item = null;
                    list.Add(item);
                }
            }
            SharpCompress.Compressor.LZMA.Log.WriteLine();
            return list;
        }

        private void ReadHeader(ArchiveDatabase db, IPasswordProvider getTextPassword)
        {
            Action<int, uint?> action = null;
            Action<int, long?> action2 = null;
            Action<int, DateTime?> action3 = null;
            Action<int, DateTime?> action4 = null;
            Action<int, DateTime?> action5 = null;
            SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadHeader --");
            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
            try
            {
                List<long> list2;
                List<uint?> list3;
                int num;
                long num6;
                int num7;
                bool flag2;
                BlockType? nullable = this.ReadId();
                if (((BlockType) nullable) == BlockType.ArchiveProperties)
                {
                    this.ReadArchiveProperties();
                    nullable = this.ReadId();
                }
                List<byte[]> dataVector = null;
                if (((BlockType) nullable) == BlockType.AdditionalStreamsInfo)
                {
                    dataVector = this.ReadAndDecodePackedStreams(db.StartPositionAfterHeader, getTextPassword);
                    nullable = this.ReadId();
                }
                if (((BlockType) nullable) == BlockType.MainStreamsInfo)
                {
                    this.ReadStreamsInfo(dataVector, out db.DataStartPosition, out db.PackSizes, out db.PackCRCs, out db.Folders, out db.NumUnpackStreamsVector, out list2, out list3);
                    db.DataStartPosition += db.StartPositionAfterHeader;
                    nullable = this.ReadId();
                }
                else
                {
                    list2 = new List<long>(db.Folders.Count);
                    list3 = new List<uint?>(db.Folders.Count);
                    db.NumUnpackStreamsVector = new List<int>(db.Folders.Count);
                    for (num = 0; num < db.Folders.Count; num++)
                    {
                        CFolder folder = db.Folders[num];
                        list2.Add(folder.GetUnpackSize());
                        list3.Add(folder.UnpackCRC);
                        db.NumUnpackStreamsVector.Add(1);
                    }
                }
                db.Files.Clear();
                if (((BlockType) nullable) == BlockType.End)
                {
                    return;
                }
                if (((BlockType) nullable) != BlockType.FilesInfo)
                {
                    throw new InvalidOperationException();
                }
                int capacity = this.ReadNum();
                SharpCompress.Compressor.LZMA.Log.WriteLine("NumFiles: " + capacity);
                db.Files = new List<CFileItem>(capacity);
                num = 0;
                while (num < capacity)
                {
                    db.Files.Add(new CFileItem());
                    num++;
                }
                BitVector vector = new BitVector(capacity);
                BitVector vector2 = null;
                BitVector vector3 = null;
                int length = 0;
                goto Label_06FC;
            Label_02DA:
                nullable = this.ReadId();
                if (((BlockType) nullable) == BlockType.End)
                {
                    goto Label_0704;
                }
                long size = (long) this.ReadNumber();
                int offset = this._currentReader.Offset;
                BlockType valueOrDefault = nullable.GetValueOrDefault();
                if (nullable.HasValue)
                {
                    switch (valueOrDefault)
                    {
                        case BlockType.EmptyStream:
                            vector = this.ReadBitVector(capacity);
                            SharpCompress.Compressor.LZMA.Log.Write("EmptyStream: ");
                            num = 0;
                            goto Label_04AD;

                        case BlockType.EmptyFile:
                            vector2 = this.ReadBitVector(length);
                            SharpCompress.Compressor.LZMA.Log.Write("EmptyFile: ");
                            num = 0;
                            goto Label_0519;

                        case BlockType.Anti:
                            vector3 = this.ReadBitVector(length);
                            SharpCompress.Compressor.LZMA.Log.Write("Anti: ");
                            num = 0;
                            goto Label_056E;

                        case BlockType.Name:
                            goto Label_0370;

                        case BlockType.CTime:
                            goto Label_05BC;

                        case BlockType.ATime:
                            goto Label_05F3;

                        case BlockType.MTime:
                            goto Label_062A;

                        case BlockType.WinAttributes:
                            goto Label_0420;

                        case BlockType.StartPos:
                            goto Label_0585;

                        case BlockType.Dummy:
                            SharpCompress.Compressor.LZMA.Log.Write("Dummy: " + size);
                            num6 = 0L;
                            goto Label_0697;
                    }
                }
                goto Label_06A5;
            Label_0370:
                using (CStreamSwitch switch2 = new CStreamSwitch())
                {
                    switch2.Set(this, dataVector);
                    SharpCompress.Compressor.LZMA.Log.Write("FileNames:");
                    num = 0;
                    while (num < db.Files.Count)
                    {
                        db.Files[num].Name = this._currentReader.ReadString();
                        SharpCompress.Compressor.LZMA.Log.Write("  " + db.Files[num].Name);
                        num++;
                    }
                    SharpCompress.Compressor.LZMA.Log.WriteLine();
                }
                goto Label_06B0;
            Label_0420:
                SharpCompress.Compressor.LZMA.Log.Write("WinAttributes:");
                if (action == null)
                {
                    action = delegate (int i, uint? attr) {
                        db.Files[i].Attrib = attr;
                        SharpCompress.Compressor.LZMA.Log.Write("  " + (attr.HasValue ? attr.Value.ToString("x8") : "n/a"));
                    };
                }
                this.ReadAttributeVector(dataVector, capacity, action);
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_06B0;
            Label_0471:
                if (vector[num])
                {
                    SharpCompress.Compressor.LZMA.Log.Write("x");
                    length++;
                }
                else
                {
                    SharpCompress.Compressor.LZMA.Log.Write(".");
                }
                num++;
            Label_04AD:
                if (num < vector.Length)
                {
                    goto Label_0471;
                }
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                vector2 = new BitVector(length);
                vector3 = new BitVector(length);
                goto Label_06B0;
            Label_04F5:
                SharpCompress.Compressor.LZMA.Log.Write(vector2[num] ? "x" : ".");
                num++;
            Label_0519:
                if (num < length)
                {
                    goto Label_04F5;
                }
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_06B0;
            Label_054A:
                SharpCompress.Compressor.LZMA.Log.Write(vector3[num] ? "x" : ".");
                num++;
            Label_056E:
                if (num < length)
                {
                    goto Label_054A;
                }
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_06B0;
            Label_0585:
                SharpCompress.Compressor.LZMA.Log.Write("StartPos:");
                if (action2 == null)
                {
                    action2 = delegate (int i, long? startPos) {
                        db.Files[i].StartPos = startPos;
                        SharpCompress.Compressor.LZMA.Log.Write("  " + (startPos.HasValue ? startPos.Value.ToString() : "n/a"));
                    };
                }
                this.ReadNumberVector(dataVector, capacity, action2);
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_06B0;
            Label_05BC:
                SharpCompress.Compressor.LZMA.Log.Write("CTime:");
                if (action3 == null)
                {
                    action3 = delegate (int i, DateTime? time) {
                        db.Files[i].CTime = time;
                        SharpCompress.Compressor.LZMA.Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
                    };
                }
                this.ReadDateTimeVector(dataVector, capacity, action3);
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_06B0;
            Label_05F3:
                SharpCompress.Compressor.LZMA.Log.Write("ATime:");
                if (action4 == null)
                {
                    action4 = delegate (int i, DateTime? time) {
                        db.Files[i].ATime = time;
                        SharpCompress.Compressor.LZMA.Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
                    };
                }
                this.ReadDateTimeVector(dataVector, capacity, action4);
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_06B0;
            Label_062A:
                SharpCompress.Compressor.LZMA.Log.Write("MTime:");
                if (action5 == null)
                {
                    action5 = delegate (int i, DateTime? time) {
                        db.Files[i].MTime = time;
                        SharpCompress.Compressor.LZMA.Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
                    };
                }
                this.ReadDateTimeVector(dataVector, capacity, action5);
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_06B0;
            Label_067B:
                if (this.ReadByte() != 0)
                {
                    throw new InvalidOperationException();
                }
                num6 += 1L;
            Label_0697:
                if (num6 < size)
                {
                    goto Label_067B;
                }
                goto Label_06B0;
            Label_06A5:
                this.SkipData(size);
            Label_06B0:
                if (((db.MajorVersion > 0) || (db.MinorVersion > 2)) && ((this._currentReader.Offset - offset) != size))
                {
                    throw new InvalidOperationException();
                }
            Label_06FC:
                flag2 = true;
                goto Label_02DA;
            Label_0704:
                num7 = 0;
                int num8 = 0;
                for (num = 0; num < capacity; num++)
                {
                    CFileItem item = db.Files[num];
                    item.HasStream = !vector[num];
                    if (item.HasStream)
                    {
                        item.IsDir = false;
                        item.IsAnti = false;
                        item.Size = list2[num8];
                        item.Crc = list3[num8];
                        num8++;
                    }
                    else
                    {
                        item.IsDir = !vector2[num7];
                        item.IsAnti = vector3[num7];
                        num7++;
                        item.Size = 0L;
                        item.Crc = null;
                    }
                }
            }
            finally
            {
                SharpCompress.Compressor.LZMA.Log.PopIndent();
            }
        }

        private BlockType? ReadId()
        {
            ulong num = this._currentReader.ReadNumber();
            if (num > 0x19L)
            {
                return null;
            }
            SharpCompress.Compressor.LZMA.Log.WriteLine("ReadId: {0}", new object[] { (BlockType) ((byte) num) });
            return new BlockType?((BlockType) ((byte) num));
        }

        internal int ReadNum()
        {
            return this._currentReader.ReadNum();
        }

        private ulong ReadNumber()
        {
            return this._currentReader.ReadNumber();
        }

        private void ReadNumberVector(List<byte[]> dataVector, int numFiles, Action<int, long?> action)
        {
            BitVector vector = this.ReadOptionalBitVector(numFiles);
            using (CStreamSwitch switch2 = new CStreamSwitch())
            {
                switch2.Set(this, dataVector);
                for (int i = 0; i < numFiles; i++)
                {
                    if (vector[i])
                    {
                        action(i, new long?((long) this.ReadUInt64()));
                    }
                    else
                    {
                        long? nullable = null;
                        action(i, nullable);
                    }
                }
            }
        }

        private BitVector ReadOptionalBitVector(int length)
        {
            if (this.ReadByte() != 0)
            {
                return new BitVector(length, true);
            }
            return this.ReadBitVector(length);
        }

        private void ReadPackInfo(out long dataOffset, out List<long> packSizes, out List<uint?> packCRCs)
        {
            SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadPackInfo --");
            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
            try
            {
                int num2;
                BlockType? nullable;
                bool flag;
                packCRCs = null;
                dataOffset = (long) this.ReadNumber();
                SharpCompress.Compressor.LZMA.Log.WriteLine("DataOffset: " + ((long) dataOffset));
                int capacity = this.ReadNum();
                SharpCompress.Compressor.LZMA.Log.WriteLine("NumPackStreams: " + capacity);
                this.WaitAttribute(BlockType.Size);
                packSizes = new List<long>(capacity);
                SharpCompress.Compressor.LZMA.Log.Write("Sizes:");
                for (num2 = 0; num2 < capacity; num2++)
                {
                    long item = (long) this.ReadNumber();
                    SharpCompress.Compressor.LZMA.Log.Write("  " + item);
                    packSizes.Add(item);
                }
                SharpCompress.Compressor.LZMA.Log.WriteLine();
                goto Label_0117;
            Label_00B7:
                nullable = this.ReadId();
                if (((BlockType) nullable) == BlockType.End)
                {
                    goto Label_011C;
                }
                if (((BlockType) nullable) == BlockType.CRC)
                {
                    packCRCs = this.ReadHashDigests(capacity);
                }
                else
                {
                    this.SkipData();
                }
            Label_0117:
                flag = true;
                goto Label_00B7;
            Label_011C:
                if (packCRCs == null)
                {
                    packCRCs = new List<uint?>(capacity);
                    for (num2 = 0; num2 < capacity; num2++)
                    {
                        uint? nullable3 = null;
                        packCRCs.Add(nullable3);
                    }
                }
            }
            finally
            {
                SharpCompress.Compressor.LZMA.Log.PopIndent();
            }
        }

        private void ReadStreamsInfo(List<byte[]> dataVector, out long dataOffset, out List<long> packSizes, out List<uint?> packCRCs, out List<CFolder> folders, out List<int> numUnpackStreamsInFolders, out List<long> unpackSizes, out List<uint?> digests)
        {
            SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadStreamsInfo --");
            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
            try
            {
                BlockType type;
                bool flag;
                dataOffset = -9223372036854775808L;
                packSizes = null;
                packCRCs = null;
                folders = null;
                numUnpackStreamsInFolders = null;
                unpackSizes = null;
                digests = null;
                goto Label_00A2;
            Label_003C:
                type = this.ReadId().GetValueOrDefault();
                if (this.ReadId().HasValue)
                {
                    switch (type)
                    {
                        case BlockType.PackInfo:
                            this.ReadPackInfo(out dataOffset, out packSizes, out packCRCs);
                            goto Label_00A2;

                        case BlockType.UnpackInfo:
                            this.ReadUnpackInfo(dataVector, out folders);
                            goto Label_00A2;

                        case BlockType.SubStreamsInfo:
                            this.ReadSubStreamsInfo(folders, out numUnpackStreamsInFolders, out unpackSizes, out digests);
                            goto Label_00A2;

                        case BlockType.End:
                            return;
                    }
                }
                throw new InvalidOperationException();
            Label_00A2:
                flag = true;
                goto Label_003C;
            }
            finally
            {
                SharpCompress.Compressor.LZMA.Log.PopIndent();
            }
        }

        private void ReadSubStreamsInfo(List<CFolder> folders, out List<int> numUnpackStreamsInFolders, out List<long> unpackSizes, out List<uint?> digests)
        {
            SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadSubStreamsInfo --");
            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
            try
            {
                BlockType? nullable;
                int num;
                int num3;
                int num5;
                bool flag;
                numUnpackStreamsInFolders = null;
                goto Label_0117;
            Label_0020:
                nullable = this.ReadId();
                BlockType? nullable2 = nullable;
                if ((((BlockType) nullable2.GetValueOrDefault()) == BlockType.NumUnpackStream) && nullable2.HasValue)
                {
                    numUnpackStreamsInFolders = new List<int>(folders.Count);
                    SharpCompress.Compressor.LZMA.Log.Write("NumUnpackStreams:");
                    for (num = 0; num < folders.Count; num++)
                    {
                        int item = this.ReadNum();
                        SharpCompress.Compressor.LZMA.Log.Write("  " + item);
                        numUnpackStreamsInFolders.Add(item);
                    }
                    SharpCompress.Compressor.LZMA.Log.WriteLine();
                }
                else
                {
                    nullable2 = nullable;
                    if ((((((BlockType) nullable2.GetValueOrDefault()) == BlockType.CRC) && nullable2.HasValue) || ((((BlockType) (nullable2 = nullable).GetValueOrDefault()) == BlockType.Size) && nullable2.HasValue)) || (((BlockType) nullable) == BlockType.End))
                    {
                        goto Label_011F;
                    }
                    this.SkipData();
                }
            Label_0117:
                flag = true;
                goto Label_0020;
            Label_011F:
                if (numUnpackStreamsInFolders == null)
                {
                    numUnpackStreamsInFolders = new List<int>(folders.Count);
                    for (num = 0; num < folders.Count; num++)
                    {
                        numUnpackStreamsInFolders.Add(1);
                    }
                }
                unpackSizes = new List<long>(folders.Count);
                for (num = 0; num < numUnpackStreamsInFolders.Count; num++)
                {
                    num3 = numUnpackStreamsInFolders[num];
                    if (num3 != 0)
                    {
                        SharpCompress.Compressor.LZMA.Log.Write("#{0} StreamSizes:", new object[] { num });
                        long num4 = 0L;
                        num5 = 1;
                        while (num5 < num3)
                        {
                            if (((BlockType) nullable) == BlockType.Size)
                            {
                                long num6 = (long) this.ReadNumber();
                                SharpCompress.Compressor.LZMA.Log.Write("  " + num6);
                                unpackSizes.Add(num6);
                                num4 += num6;
                            }
                            num5++;
                        }
                        unpackSizes.Add(folders[num].GetUnpackSize() - num4);
                        SharpCompress.Compressor.LZMA.Log.WriteLine("  -  rest: " + Enumerable.Last<long>(unpackSizes));
                    }
                }
                if (((BlockType) nullable) == BlockType.Size)
                {
                    nullable = this.ReadId();
                }
                int count = 0;
                int capacity = 0;
                for (num = 0; num < folders.Count; num++)
                {
                    num3 = numUnpackStreamsInFolders[num];
                    if (!((num3 == 1) && folders[num].UnpackCRCDefined))
                    {
                        count += num3;
                    }
                    capacity += num3;
                }
                digests = null;
                goto Label_0453;
            Label_02E7:
                nullable2 = nullable;
                if ((((BlockType) nullable2.GetValueOrDefault()) == BlockType.CRC) && nullable2.HasValue)
                {
                    digests = new List<uint?>(capacity);
                    List<uint?> list = this.ReadHashDigests(count);
                    int num9 = 0;
                    for (num = 0; num < folders.Count; num++)
                    {
                        num3 = numUnpackStreamsInFolders[num];
                        CFolder folder = folders[num];
                        if ((num3 == 1) && folder.UnpackCRCDefined)
                        {
                            digests.Add(new uint?(folder.UnpackCRC.Value));
                        }
                        else
                        {
                            num5 = 0;
                            while (num5 < num3)
                            {
                                digests.Add(list[num9]);
                                num5++;
                                num9++;
                            }
                        }
                    }
                    if ((num9 != count) || (capacity != digests.Count))
                    {
                        Debugger.Break();
                    }
                }
                else
                {
                    if (((BlockType) nullable) == BlockType.End)
                    {
                        if (digests == null)
                        {
                            digests = new List<uint?>(capacity);
                            for (num = 0; num < capacity; num++)
                            {
                                uint? nullable3 = null;
                                digests.Add(nullable3);
                            }
                        }
                        return;
                    }
                    this.SkipData();
                }
                nullable = this.ReadId();
            Label_0453:
                flag = true;
                goto Label_02E7;
            }
            finally
            {
                SharpCompress.Compressor.LZMA.Log.PopIndent();
            }
        }

        private uint ReadUInt32()
        {
            return this._currentReader.ReadUInt32();
        }

        private ulong ReadUInt64()
        {
            return this._currentReader.ReadUInt64();
        }

        private void ReadUnpackInfo(List<byte[]> dataVector, out List<CFolder> folders)
        {
            SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadUnpackInfo --");
            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
            try
            {
                int num3;
                BlockType? nullable;
                bool flag;
                this.WaitAttribute(BlockType.Folder);
                int capacity = this.ReadNum();
                SharpCompress.Compressor.LZMA.Log.WriteLine("NumFolders: {0}", new object[] { capacity });
                using (CStreamSwitch switch2 = new CStreamSwitch())
                {
                    switch2.Set(this, dataVector);
                    folders = new List<CFolder>(capacity);
                    int num2 = 0;
                    num3 = 0;
                    while (num3 < capacity)
                    {
                        CFolder folder2 = new CFolder();
                        folder2.FirstPackStreamId = num2;
                        CFolder item = folder2;
                        folders.Add(item);
                        this.GetNextFolderItem(item);
                        num2 += item.PackStreams.Count;
                        num3++;
                    }
                }
                this.WaitAttribute(BlockType.CodersUnpackSize);
                SharpCompress.Compressor.LZMA.Log.WriteLine("UnpackSizes:");
                for (num3 = 0; num3 < capacity; num3++)
                {
                    CFolder folder3 = folders[num3];
                    SharpCompress.Compressor.LZMA.Log.Write("  #" + num3 + ":");
                    int numOutStreams = folder3.GetNumOutStreams();
                    for (int i = 0; i < numOutStreams; i++)
                    {
                        long num6 = (long) this.ReadNumber();
                        SharpCompress.Compressor.LZMA.Log.Write("  " + num6);
                        folder3.UnpackSizes.Add(num6);
                    }
                    SharpCompress.Compressor.LZMA.Log.WriteLine();
                }
                goto Label_01F9;
            Label_016F:
                nullable = this.ReadId();
                if (((BlockType) nullable) == BlockType.End)
                {
                    return;
                }
                if (((BlockType) nullable) == BlockType.CRC)
                {
                    List<uint?> list = this.ReadHashDigests(capacity);
                    for (num3 = 0; num3 < capacity; num3++)
                    {
                        folders[num3].UnpackCRC = list[num3];
                    }
                }
                else
                {
                    this.SkipData();
                }
            Label_01F9:
                flag = true;
                goto Label_016F;
            }
            finally
            {
                SharpCompress.Compressor.LZMA.Log.PopIndent();
            }
        }

        private void SkipData()
        {
            this._currentReader.SkipData();
        }

        private void SkipData(long size)
        {
            this._currentReader.SkipData(size);
        }

        private DateTime TranslateTime(long time)
        {
            return DateTime.FromFileTimeUtc(time).ToLocalTime();
        }

        private DateTime? TranslateTime(long? time)
        {
            if (time.HasValue)
            {
                return new DateTime?(this.TranslateTime(time.Value));
            }
            return null;
        }

        private void WaitAttribute(BlockType attribute)
        {
            while (true)
            {
                BlockType? nullable = this.ReadId();
                BlockType? nullable2 = nullable;
                BlockType type = attribute;
                if ((((BlockType) nullable2.GetValueOrDefault()) == type) && nullable2.HasValue)
                {
                    return;
                }
                if (((BlockType) nullable) == BlockType.End)
                {
                    throw new InvalidOperationException();
                }
                this.SkipData();
            }
        }

        internal class CExtractFolderInfo
        {
            internal List<bool> ExtractStatuses = new List<bool>();
            internal int FileIndex;
            internal int FolderIndex;

            internal CExtractFolderInfo(int fileIndex, int folderIndex)
            {
                this.FileIndex = fileIndex;
                this.FolderIndex = folderIndex;
                if (fileIndex != -1)
                {
                    this.ExtractStatuses.Add(true);
                }
            }
        }

        private class FolderUnpackStream : Stream
        {
            private int _currentIndex;
            private ArchiveDatabase _db;
            private List<bool> _extractStatuses;
            private int _otherIndex;
            private long _rem;
            private int _startIndex;
            private Stream _stream;

            public FolderUnpackStream(ArchiveDatabase db, int p, int startIndex, List<bool> list)
            {
                this._db = db;
                this._otherIndex = p;
                this._startIndex = startIndex;
                this._extractStatuses = list;
            }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            private void OpenFile()
            {
                bool flag = !this._extractStatuses[this._currentIndex];
                int num = this._startIndex + this._currentIndex;
                int num2 = this._otherIndex + num;
                SharpCompress.Compressor.LZMA.Log.WriteLine(this._db.Files[num].Name);
                if (this._db.Files[num].CrcDefined)
                {
                    this._stream = new CrcCheckStream(this._db.Files[num].Crc.Value);
                }
                else
                {
                    this._stream = new MemoryStream();
                }
                this._rem = this._db.Files[num].Size;
            }

            private void ProcessEmptyFiles()
            {
                while ((this._currentIndex < this._extractStatuses.Count) && (this._db.Files[this._startIndex + this._currentIndex].Size == 0L))
                {
                    this.OpenFile();
                    this._stream.Dispose();
                    this._stream = null;
                    this._currentIndex++;
                }
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

            public override void Write(byte[] buffer, int offset, int count)
            {
                while (count != 0)
                {
                    if (this._stream != null)
                    {
                        int num = count;
                        if (num > this._rem)
                        {
                            num = (int) this._rem;
                        }
                        this._stream.Write(buffer, offset, num);
                        count -= num;
                        this._rem -= num;
                        offset += num;
                        if (this._rem == 0L)
                        {
                            this._stream.Dispose();
                            this._stream = null;
                            this._currentIndex++;
                            this.ProcessEmptyFiles();
                        }
                    }
                    else
                    {
                        this.ProcessEmptyFiles();
                        if (this._currentIndex == this._extractStatuses.Count)
                        {
                            Debugger.Break();
                            throw new NotSupportedException();
                        }
                        this.OpenFile();
                    }
                }
            }

            public override bool CanRead
            {
                get
                {
                    return true;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return false;
                }
            }

            public override long Length
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public override long Position
            {
                get
                {
                    throw new NotSupportedException();
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}

