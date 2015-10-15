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

    //internal class ArchiveReader
    //{
    //    private Dictionary<int, Stream> _cachedStreams = new Dictionary<int, Stream>();
    //    internal DataReader _currentReader;
    //    internal byte[] _header;
    //    internal Stack<DataReader> _readerStack = new Stack<DataReader>();
    //    internal Stream _stream;
    //    internal long _streamEnding;
    //    internal long _streamOrigin;

    //    internal void AddByteStream(byte[] buffer, int offset, int length)
    //    {
    //        this._readerStack.Push(this._currentReader);
    //        this._currentReader = new DataReader(buffer, offset, length);
    //    }

    //    public void Close()
    //    {
    //        if (this._stream != null)
    //        {
    //            this._stream.Dispose();
    //        }
    //        foreach (Stream stream in this._cachedStreams.Values)
    //        {
    //            stream.Dispose();
    //        }
    //        this._cachedStreams.Clear();
    //    }

    //    internal void DeleteByteStream()
    //    {
    //        this._currentReader = this._readerStack.Pop();
    //    }

    //    public void Extract(ArchiveDatabase _db, int[] indices, IPasswordProvider pw)
    //    {
    //        int count;
    //        bool flag = indices == null;
    //        if (flag)
    //        {
    //            count = _db.Files.Count;
    //        }
    //        else
    //        {
    //            count = indices.Length;
    //        }
    //        if (count != 0)
    //        {
    //            int folderIndex;
    //            int num5;
    //            List<CExtractFolderInfo> source = new List<CExtractFolderInfo>();
    //            for (int i = 0; i < count; i++)
    //            {
    //                int fileIndex = flag ? i : indices[i];
    //                folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
    //                if (folderIndex == -1)
    //                {
    //                    source.Add(new CExtractFolderInfo(fileIndex, -1));
    //                }
    //                else
    //                {
    //                    if ((source.Count == 0) || (folderIndex != Enumerable.Last<CExtractFolderInfo>(source).FolderIndex))
    //                    {
    //                        source.Add(new CExtractFolderInfo(-1, folderIndex));
    //                    }
    //                    CExtractFolderInfo info = Enumerable.Last<CExtractFolderInfo>(source);
    //                    num5 = _db.FolderStartFileIndex[folderIndex];
    //                    for (int j = info.ExtractStatuses.Count; j <= (fileIndex - num5); j++)
    //                    {
    //                        info.ExtractStatuses.Add(j == (fileIndex - num5));
    //                    }
    //                }
    //            }
    //            foreach (CExtractFolderInfo info in source)
    //            {
    //                int num10;
    //                bool flag2;
    //                if (info.FileIndex != -1)
    //                {
    //                    num5 = info.FileIndex;
    //                }
    //                else
    //                {
    //                    num5 = _db.FolderStartFileIndex[info.FolderIndex];
    //                }
    //                FolderUnpackStream stream = new FolderUnpackStream(_db, 0, num5, info.ExtractStatuses);
    //                if (info.FileIndex != -1)
    //                {
    //                    continue;
    //                }
    //                folderIndex = info.FolderIndex;
    //                CFolder folder = _db.Folders[folderIndex];
    //                int firstPackStreamId = _db.Folders[folderIndex].FirstPackStreamId;
    //                long folderStreamPos = _db.GetFolderStreamPos(folder, 0);
    //                List<long> list2 = new List<long>();
    //                for (int k = 0; k < folder.PackStreams.Count; k++)
    //                {
    //                    list2.Add(_db.PackSizes[firstPackStreamId + k]);
    //                }
    //                Stream stream2 = DecoderStreamHelper.CreateDecoderStream(this._stream, folderStreamPos, list2.ToArray(), folder, pw);
    //                byte[] buffer = new byte[0x1000];
    //                goto Label_0252;
    //            Label_0223:
    //                num10 = stream2.Read(buffer, 0, buffer.Length);
    //                if (num10 == 0)
    //                {
    //                    continue;
    //                }
    //                stream.Write(buffer, 0, num10);
    //            Label_0252:
    //                flag2 = true;
    //                goto Label_0223;
    //            }
    //        }
    //    }

    //    private Stream GetCachedDecoderStream(ArchiveDatabase _db, int folderIndex, IPasswordProvider pw)
    //    {
    //        Stream stream;
    //        if (!this._cachedStreams.TryGetValue(folderIndex, out stream))
    //        {
    //            CFolder folder = _db.Folders[folderIndex];
    //            int firstPackStreamId = _db.Folders[folderIndex].FirstPackStreamId;
    //            long folderStreamPos = _db.GetFolderStreamPos(folder, 0);
    //            List<long> list = new List<long>();
    //            for (int i = 0; i < folder.PackStreams.Count; i++)
    //            {
    //                list.Add(_db.PackSizes[firstPackStreamId + i]);
    //            }
    //            stream = DecoderStreamHelper.CreateDecoderStream(this._stream, folderStreamPos, list.ToArray(), folder, pw);
    //            this._cachedStreams.Add(folderIndex, stream);
    //        }
    //        return stream;
    //    }

    //    public int GetFileIndex(ArchiveDatabase db, CFileItem item)
    //    {
    //        return db.Files.IndexOf(item);
    //    }

    //    public IEnumerable<CFileItem> GetFiles(ArchiveDatabase db)
    //    {
    //        return db.Files;
    //    }

    //    private void GetNextFolderItem(CFolder folder)
    //    {
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("-- GetNextFolderItem --");
    //        SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //        try
    //        {
    //            int num4;
    //            int capacity = this.ReadNum();
    //            SharpCompress.Compressor.LZMA.Log.WriteLine("NumCoders: " + capacity);
    //            folder.Coders = new List<CCoderInfo>(capacity);
    //            int num2 = 0;
    //            int num3 = 0;
    //            for (num4 = 0; num4 < capacity; num4++)
    //            {
    //                SharpCompress.Compressor.LZMA.Log.WriteLine("-- Coder --");
    //                SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //                try
    //                {
    //                    CCoderInfo item = new CCoderInfo();
    //                    folder.Coders.Add(item);
    //                    byte num5 = this.ReadByte();
    //                    int length = num5 & 15;
    //                    byte[] longID = new byte[length];
    //                    this.ReadBytes(longID, 0, length);
    //                    SharpCompress.Compressor.LZMA.Log.WriteLine("MethodId: " + string.Join("", Enumerable.ToArray<string>(Enumerable.Select<int, string>(Enumerable.Range(0, length), delegate (int x) {
    //                        return longID[x].ToString("x2");
    //                    }))));
    //                    if (length > 8)
    //                    {
    //                        throw new NotSupportedException();
    //                    }
    //                    ulong id = 0L;
    //                    for (int i = 0; i < length; i++)
    //                    {
    //                        id |= longID[(length - 1) - i] << (8 * i);
    //                    }
    //                    item.MethodId = new CMethodId(id);
    //                    if ((num5 & 0x10) != 0)
    //                    {
    //                        item.NumInStreams = this.ReadNum();
    //                        item.NumOutStreams = this.ReadNum();
    //                        SharpCompress.Compressor.LZMA.Log.WriteLine(string.Concat(new object[] { "Complex Stream (In: ", item.NumInStreams, " - Out: ", item.NumOutStreams, ")" }));
    //                    }
    //                    else
    //                    {
    //                        SharpCompress.Compressor.LZMA.Log.WriteLine("Simple Stream (In: 1 - Out: 1)");
    //                        item.NumInStreams = 1;
    //                        item.NumOutStreams = 1;
    //                    }
    //                    if ((num5 & 0x20) != 0)
    //                    {
    //                        int num9 = this.ReadNum();
    //                        item.Props = new byte[num9];
    //                        this.ReadBytes(item.Props, 0, num9);
    //                        SharpCompress.Compressor.LZMA.Log.WriteLine("Settings: " + string.Join("", Enumerable.ToArray<string>(Enumerable.Select<byte, string>(item.Props, delegate (byte bt) {
    //                            return bt.ToString("x2");
    //                        }))));
    //                    }
    //                    if ((num5 & 0x80) != 0)
    //                    {
    //                        throw new NotSupportedException();
    //                    }
    //                    num2 += item.NumInStreams;
    //                    num3 += item.NumOutStreams;
    //                }
    //                finally
    //                {
    //                    SharpCompress.Compressor.LZMA.Log.PopIndent();
    //                }
    //            }
    //            int num10 = num3 - 1;
    //            folder.BindPairs = new List<CBindPair>(num10);
    //            SharpCompress.Compressor.LZMA.Log.WriteLine("BindPairs: " + num10);
    //            SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //            for (num4 = 0; num4 < num10; num4++)
    //            {
    //                CBindPair pair = new CBindPair();
    //                pair.InIndex = this.ReadNum();
    //                pair.OutIndex = this.ReadNum();
    //                folder.BindPairs.Add(pair);
    //                SharpCompress.Compressor.LZMA.Log.WriteLine(string.Concat(new object[] { "#", num4, " - In: ", pair.InIndex, " - Out: ", pair.OutIndex }));
    //            }
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //            if (num2 < num10)
    //            {
    //                throw new NotSupportedException();
    //            }
    //            int num11 = num2 - num10;
    //            if (num11 == 1)
    //            {
    //                for (num4 = 0; num4 < num2; num4++)
    //                {
    //                    if (folder.FindBindPairForInStream(num4) < 0)
    //                    {
    //                        SharpCompress.Compressor.LZMA.Log.WriteLine("Single PackStream: #" + num4);
    //                        folder.PackStreams.Add(num4);
    //                        break;
    //                    }
    //                }
    //                if (folder.PackStreams.Count != 1)
    //                {
    //                    throw new NotSupportedException();
    //                }
    //            }
    //            else
    //            {
    //                SharpCompress.Compressor.LZMA.Log.WriteLine("Multiple PackStreams ...");
    //                SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //                for (num4 = 0; num4 < num11; num4++)
    //                {
    //                    int num12 = this.ReadNum();
    //                    SharpCompress.Compressor.LZMA.Log.WriteLine(string.Concat(new object[] { "#", num4, " - ", num12 }));
    //                    folder.PackStreams.Add(num12);
    //                }
    //                SharpCompress.Compressor.LZMA.Log.PopIndent();
    //            }
    //        }
    //        finally
    //        {
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //        }
    //    }

    //    public void Open(Stream stream)
    //    {
    //        int num2;
    //        this.Close();
    //        this._streamOrigin = stream.Position;
    //        this._streamEnding = stream.Length;
    //        this._header = new byte[0x20];
    //        for (int i = 0; i < 0x20; i += num2)
    //        {
    //            num2 = stream.Read(this._header, i, 0x20 - i);
    //            if (num2 == 0)
    //            {
    //                throw new EndOfStreamException();
    //            }
    //        }
    //        this._stream = stream;
    //    }

    //    public Stream OpenStream(ArchiveDatabase _db, int fileIndex, IPasswordProvider pw)
    //    {
    //        int folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
    //        int num2 = _db.NumUnpackStreamsVector[folderIndex];
    //        int num3 = _db.FolderStartFileIndex[folderIndex];
    //        if ((num3 > fileIndex) || ((fileIndex - num3) >= num2))
    //        {
    //            throw new InvalidOperationException();
    //        }
    //        int num4 = fileIndex - num3;
    //        long num5 = 0L;
    //        for (int i = 0; i < num4; i++)
    //        {
    //            num5 += _db.Files[num3 + i].Size;
    //        }
    //        Stream stream = this.GetCachedDecoderStream(_db, folderIndex, pw);
    //        stream.Position = num5;
    //        return new ReadOnlySubStream(stream, _db.Files[fileIndex].Size);
    //    }

    //    private List<byte[]> ReadAndDecodePackedStreams(long baseOffset, IPasswordProvider pass)
    //    {
    //        List<byte[]> list8;
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadAndDecodePackedStreams --");
    //        SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //        try
    //        {
    //            long num;
    //            List<long> list;
    //            List<uint?> list2;
    //            List<CFolder> list3;
    //            List<int> list4;
    //            List<long> list5;
    //            List<uint?> list6;
    //            this.ReadStreamsInfo(null, out num, out list, out list2, out list3, out list4, out list5, out list6);
    //            num += baseOffset;
    //            List<byte[]> list7 = new List<byte[]>(list3.Count);
    //            int num2 = 0;
    //            foreach (CFolder folder in list3)
    //            {
    //                long startPos = num;
    //                long[] packSizes = new long[folder.PackStreams.Count];
    //                for (int i = 0; i < packSizes.Length; i++)
    //                {
    //                    long num5 = list[num2 + i];
    //                    packSizes[i] = num5;
    //                    num += num5;
    //                }
    //                Stream stream = DecoderStreamHelper.CreateDecoderStream(this._stream, startPos, packSizes, folder, pass);
    //                int unpackSize = (int) folder.GetUnpackSize();
    //                byte[] buffer = new byte[unpackSize];
    //                Utils.ReadExact(stream, buffer, 0, buffer.Length);
    //                if (stream.ReadByte() >= 0)
    //                {
    //                    throw new InvalidOperationException("Decoded stream is longer than expected.");
    //                }
    //                list7.Add(buffer);
    //                if (folder.UnpackCRCDefined)
    //                {
    //                    uint num7 = CRC.Finish(CRC.Update(uint.MaxValue, buffer, 0, unpackSize));
    //                    if (num7 != folder.UnpackCRC)
    //                    {
    //                        throw new InvalidOperationException("Decoded stream does not match expected CRC.");
    //                    }
    //                }
    //            }
    //            list8 = list7;
    //        }
    //        finally
    //        {
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //        }
    //        return list8;
    //    }

    //    private void ReadArchiveProperties()
    //    {
    //        while (true)
    //        {
    //            BlockType? nullable = this.ReadId();
    //            if ((((BlockType) nullable.GetValueOrDefault()) == BlockType.End) && nullable.HasValue)
    //            {
    //                return;
    //            }
    //            this.SkipData();
    //        }
    //    }

    //    private void ReadAttributeVector(List<byte[]> dataVector, int numFiles, Action<int, uint?> action)
    //    {
    //        BitVector vector = this.ReadOptionalBitVector(numFiles);
    //        using (CStreamSwitch switch2 = new CStreamSwitch())
    //        {
    //            switch2.Set(this, dataVector);
    //            for (int i = 0; i < numFiles; i++)
    //            {
    //                if (vector[i])
    //                {
    //                    action(i, new uint?(this.ReadUInt32()));
    //                }
    //                else
    //                {
    //                    uint? nullable = null;
    //                    action(i, nullable);
    //                }
    //            }
    //        }
    //    }

    //    private BitVector ReadBitVector(int length)
    //    {
    //        BitVector vector = new BitVector(length);
    //        byte num = 0;
    //        byte num2 = 0;
    //        for (int i = 0; i < length; i++)
    //        {
    //            if (num2 == 0)
    //            {
    //                num = this.ReadByte();
    //                num2 = 0x80;
    //            }
    //            if ((num & num2) != 0)
    //            {
    //                vector.SetBit(i);
    //            }
    //            num2 = (byte) (num2 >> 1);
    //        }
    //        return vector;
    //    }

    //    internal byte ReadByte()
    //    {
    //        return this._currentReader.ReadByte();
    //    }

    //    private void ReadBytes(byte[] buffer, int offset, int length)
    //    {
    //        this._currentReader.ReadBytes(buffer, offset, length);
    //    }

    //    public ArchiveDatabase ReadDatabase(IPasswordProvider pass)
    //    {
    //        ArchiveDatabase db = new ArchiveDatabase();
    //        db.Clear();
    //        db.MajorVersion = this._header[6];
    //        db.MinorVersion = this._header[7];
    //        if (db.MajorVersion != 0)
    //        {
    //            throw new InvalidOperationException();
    //        }
    //        uint num = DataReader.Get32(this._header, 8);
    //        long num2 = (long) DataReader.Get64(this._header, 12);
    //        long num3 = (long) DataReader.Get64(this._header, 20);
    //        uint num4 = DataReader.Get32(this._header, 0x1c);
    //        uint maxValue = uint.MaxValue;
    //        if (CRC.Finish(CRC.Update(CRC.Update(CRC.Update(maxValue, num2), num3), num4)) != num)
    //        {
    //            throw new InvalidOperationException();
    //        }
    //        db.StartPositionAfterHeader = this._streamOrigin + 0x20L;
    //        if (num3 == 0L)
    //        {
    //            db.Fill();
    //            return db;
    //        }
    //        if (((num2 < 0L) || (num3 < 0L)) || (num3 > 0x7fffffffL))
    //        {
    //            throw new InvalidOperationException();
    //        }
    //        if (num2 > (this._streamEnding - db.StartPositionAfterHeader))
    //        {
    //            throw new IndexOutOfRangeException();
    //        }
    //        this._stream.Seek(num2, SeekOrigin.Current);
    //        byte[] buffer = new byte[num3];
    //        Utils.ReadExact(this._stream, buffer, 0, buffer.Length);
    //        if (CRC.Finish(CRC.Update(uint.MaxValue, buffer, 0, buffer.Length)) != num4)
    //        {
    //            throw new InvalidOperationException();
    //        }
    //        using (CStreamSwitch switch2 = new CStreamSwitch())
    //        {
    //            switch2.Set(this, buffer);
    //            BlockType? nullable = this.ReadId();
    //            if (((BlockType) nullable) != BlockType.Header)
    //            {
    //                if (((BlockType) nullable) != BlockType.EncodedHeader)
    //                {
    //                    throw new InvalidOperationException();
    //                }
    //                List<byte[]> list = this.ReadAndDecodePackedStreams(db.StartPositionAfterHeader, pass);
    //                if (list.Count == 0)
    //                {
    //                    db.Fill();
    //                    return db;
    //                }
    //                if (list.Count != 1)
    //                {
    //                    throw new InvalidOperationException();
    //                }
    //                switch2.Set(this, list[0]);
    //                if (((BlockType) this.ReadId()) != BlockType.Header)
    //                {
    //                    throw new InvalidOperationException();
    //                }
    //            }
    //            this.ReadHeader(db, pass);
    //        }
    //        db.Fill();
    //        return db;
    //    }

    //    private void ReadDateTimeVector(List<byte[]> dataVector, int numFiles, Action<int, DateTime?> action)
    //    {
    //        this.ReadNumberVector(dataVector, numFiles, delegate (int index, long? value) {
    //            action(index, this.TranslateTime(value));
    //        });
    //    }

    //    private List<uint?> ReadHashDigests(int count)
    //    {
    //        SharpCompress.Compressor.LZMA.Log.Write("ReadHashDigests:");
    //        BitVector vector = this.ReadOptionalBitVector(count);
    //        List<uint?> list = new List<uint?>(count);
    //        for (int i = 0; i < count; i++)
    //        {
    //            if (vector[i])
    //            {
    //                uint num2 = this.ReadUInt32();
    //                SharpCompress.Compressor.LZMA.Log.Write("  " + num2.ToString("x8"));
    //                list.Add(new uint?(num2));
    //            }
    //            else
    //            {
    //                SharpCompress.Compressor.LZMA.Log.Write("  ########");
    //                uint? item = null;
    //                list.Add(item);
    //            }
    //        }
    //        SharpCompress.Compressor.LZMA.Log.WriteLine();
    //        return list;
    //    }

    //    private void ReadHeader(ArchiveDatabase db, IPasswordProvider getTextPassword)
    //    {
    //        Action<int, uint?> action = null;
    //        Action<int, long?> action2 = null;
    //        Action<int, DateTime?> action3 = null;
    //        Action<int, DateTime?> action4 = null;
    //        Action<int, DateTime?> action5 = null;
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadHeader --");
    //        SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //        try
    //        {
    //            List<long> list2;
    //            List<uint?> list3;
    //            int num;
    //            long num6;
    //            int num7;
    //            bool flag2;
    //            BlockType? nullable = this.ReadId();
    //            if (((BlockType) nullable) == BlockType.ArchiveProperties)
    //            {
    //                this.ReadArchiveProperties();
    //                nullable = this.ReadId();
    //            }
    //            List<byte[]> dataVector = null;
    //            if (((BlockType) nullable) == BlockType.AdditionalStreamsInfo)
    //            {
    //                dataVector = this.ReadAndDecodePackedStreams(db.StartPositionAfterHeader, getTextPassword);
    //                nullable = this.ReadId();
    //            }
    //            if (((BlockType) nullable) == BlockType.MainStreamsInfo)
    //            {
    //                this.ReadStreamsInfo(dataVector, out db.DataStartPosition, out db.PackSizes, out db.PackCRCs, out db.Folders, out db.NumUnpackStreamsVector, out list2, out list3);
    //                db.DataStartPosition += db.StartPositionAfterHeader;
    //                nullable = this.ReadId();
    //            }
    //            else
    //            {
    //                list2 = new List<long>(db.Folders.Count);
    //                list3 = new List<uint?>(db.Folders.Count);
    //                db.NumUnpackStreamsVector = new List<int>(db.Folders.Count);
    //                for (num = 0; num < db.Folders.Count; num++)
    //                {
    //                    CFolder folder = db.Folders[num];
    //                    list2.Add(folder.GetUnpackSize());
    //                    list3.Add(folder.UnpackCRC);
    //                    db.NumUnpackStreamsVector.Add(1);
    //                }
    //            }
    //            db.Files.Clear();
    //            if (((BlockType) nullable) == BlockType.End)
    //            {
    //                return;
    //            }
    //            if (((BlockType) nullable) != BlockType.FilesInfo)
    //            {
    //                throw new InvalidOperationException();
    //            }
    //            int capacity = this.ReadNum();
    //            SharpCompress.Compressor.LZMA.Log.WriteLine("NumFiles: " + capacity);
    //            db.Files = new List<CFileItem>(capacity);
    //            num = 0;
    //            while (num < capacity)
    //            {
    //                db.Files.Add(new CFileItem());
    //                num++;
    //            }
    //            BitVector vector = new BitVector(capacity);
    //            BitVector vector2 = null;
    //            BitVector vector3 = null;
    //            int length = 0;
    //            goto Label_06FC;
    //        Label_02DA:
    //            nullable = this.ReadId();
    //            if (((BlockType) nullable) == BlockType.End)
    //            {
    //                goto Label_0704;
    //            }
    //            long size = (long) this.ReadNumber();
    //            int offset = this._currentReader.Offset;
    //            BlockType valueOrDefault = nullable.GetValueOrDefault();
    //            if (nullable.HasValue)
    //            {
    //                switch (valueOrDefault)
    //                {
    //                    case BlockType.EmptyStream:
    //                        vector = this.ReadBitVector(capacity);
    //                        SharpCompress.Compressor.LZMA.Log.Write("EmptyStream: ");
    //                        num = 0;
    //                        goto Label_04AD;

    //                    case BlockType.EmptyFile:
    //                        vector2 = this.ReadBitVector(length);
    //                        SharpCompress.Compressor.LZMA.Log.Write("EmptyFile: ");
    //                        num = 0;
    //                        goto Label_0519;

    //                    case BlockType.Anti:
    //                        vector3 = this.ReadBitVector(length);
    //                        SharpCompress.Compressor.LZMA.Log.Write("Anti: ");
    //                        num = 0;
    //                        goto Label_056E;

    //                    case BlockType.Name:
    //                        goto Label_0370;

    //                    case BlockType.CTime:
    //                        goto Label_05BC;

    //                    case BlockType.ATime:
    //                        goto Label_05F3;

    //                    case BlockType.MTime:
    //                        goto Label_062A;

    //                    case BlockType.WinAttributes:
    //                        goto Label_0420;

    //                    case BlockType.StartPos:
    //                        goto Label_0585;

    //                    case BlockType.Dummy:
    //                        SharpCompress.Compressor.LZMA.Log.Write("Dummy: " + size);
    //                        num6 = 0L;
    //                        goto Label_0697;
    //                }
    //            }
    //            goto Label_06A5;
    //        Label_0370:
    //            using (CStreamSwitch switch2 = new CStreamSwitch())
    //            {
    //                switch2.Set(this, dataVector);
    //                SharpCompress.Compressor.LZMA.Log.Write("FileNames:");
    //                num = 0;
    //                while (num < db.Files.Count)
    //                {
    //                    db.Files[num].Name = this._currentReader.ReadString();
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + db.Files[num].Name);
    //                    num++;
    //                }
    //                SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            }
    //            goto Label_06B0;
    //        Label_0420:
    //            SharpCompress.Compressor.LZMA.Log.Write("WinAttributes:");
    //            if (action == null)
    //            {
    //                action = delegate (int i, uint? attr) {
    //                    db.Files[i].Attrib = attr;
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + (attr.HasValue ? attr.Value.ToString("x8") : "n/a"));
    //                };
    //            }
    //            this.ReadAttributeVector(dataVector, capacity, action);
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_06B0;
    //        Label_0471:
    //            if (vector[num])
    //            {
    //                SharpCompress.Compressor.LZMA.Log.Write("x");
    //                length++;
    //            }
    //            else
    //            {
    //                SharpCompress.Compressor.LZMA.Log.Write(".");
    //            }
    //            num++;
    //        Label_04AD:
    //            if (num < vector.Length)
    //            {
    //                goto Label_0471;
    //            }
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            vector2 = new BitVector(length);
    //            vector3 = new BitVector(length);
    //            goto Label_06B0;
    //        Label_04F5:
    //            SharpCompress.Compressor.LZMA.Log.Write(vector2[num] ? "x" : ".");
    //            num++;
    //        Label_0519:
    //            if (num < length)
    //            {
    //                goto Label_04F5;
    //            }
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_06B0;
    //        Label_054A:
    //            SharpCompress.Compressor.LZMA.Log.Write(vector3[num] ? "x" : ".");
    //            num++;
    //        Label_056E:
    //            if (num < length)
    //            {
    //                goto Label_054A;
    //            }
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_06B0;
    //        Label_0585:
    //            SharpCompress.Compressor.LZMA.Log.Write("StartPos:");
    //            if (action2 == null)
    //            {
    //                action2 = delegate (int i, long? startPos) {
    //                    db.Files[i].StartPos = startPos;
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + (startPos.HasValue ? startPos.Value.ToString() : "n/a"));
    //                };
    //            }
    //            this.ReadNumberVector(dataVector, capacity, action2);
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_06B0;
    //        Label_05BC:
    //            SharpCompress.Compressor.LZMA.Log.Write("CTime:");
    //            if (action3 == null)
    //            {
    //                action3 = delegate (int i, DateTime? time) {
    //                    db.Files[i].CTime = time;
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
    //                };
    //            }
    //            this.ReadDateTimeVector(dataVector, capacity, action3);
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_06B0;
    //        Label_05F3:
    //            SharpCompress.Compressor.LZMA.Log.Write("ATime:");
    //            if (action4 == null)
    //            {
    //                action4 = delegate (int i, DateTime? time) {
    //                    db.Files[i].ATime = time;
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
    //                };
    //            }
    //            this.ReadDateTimeVector(dataVector, capacity, action4);
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_06B0;
    //        Label_062A:
    //            SharpCompress.Compressor.LZMA.Log.Write("MTime:");
    //            if (action5 == null)
    //            {
    //                action5 = delegate (int i, DateTime? time) {
    //                    db.Files[i].MTime = time;
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
    //                };
    //            }
    //            this.ReadDateTimeVector(dataVector, capacity, action5);
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_06B0;
    //        Label_067B:
    //            if (this.ReadByte() != 0)
    //            {
    //                throw new InvalidOperationException();
    //            }
    //            num6 += 1L;
    //        Label_0697:
    //            if (num6 < size)
    //            {
    //                goto Label_067B;
    //            }
    //            goto Label_06B0;
    //        Label_06A5:
    //            this.SkipData(size);
    //        Label_06B0:
    //            if (((db.MajorVersion > 0) || (db.MinorVersion > 2)) && ((this._currentReader.Offset - offset) != size))
    //            {
    //                throw new InvalidOperationException();
    //            }
    //        Label_06FC:
    //            flag2 = true;
    //            goto Label_02DA;
    //        Label_0704:
    //            num7 = 0;
    //            int num8 = 0;
    //            for (num = 0; num < capacity; num++)
    //            {
    //                CFileItem item = db.Files[num];
    //                item.HasStream = !vector[num];
    //                if (item.HasStream)
    //                {
    //                    item.IsDir = false;
    //                    item.IsAnti = false;
    //                    item.Size = list2[num8];
    //                    item.Crc = list3[num8];
    //                    num8++;
    //                }
    //                else
    //                {
    //                    item.IsDir = !vector2[num7];
    //                    item.IsAnti = vector3[num7];
    //                    num7++;
    //                    item.Size = 0L;
    //                    item.Crc = null;
    //                }
    //            }
    //        }
    //        finally
    //        {
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //        }
    //    }

    //    private BlockType? ReadId()
    //    {
    //        ulong num = this._currentReader.ReadNumber();
    //        if (num > 0x19L)
    //        {
    //            return null;
    //        }
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("ReadId: {0}", new object[] { (BlockType) ((byte) num) });
    //        return new BlockType?((BlockType) ((byte) num));
    //    }

    //    internal int ReadNum()
    //    {
    //        return this._currentReader.ReadNum();
    //    }

    //    private ulong ReadNumber()
    //    {
    //        return this._currentReader.ReadNumber();
    //    }

    //    private void ReadNumberVector(List<byte[]> dataVector, int numFiles, Action<int, long?> action)
    //    {
    //        BitVector vector = this.ReadOptionalBitVector(numFiles);
    //        using (CStreamSwitch switch2 = new CStreamSwitch())
    //        {
    //            switch2.Set(this, dataVector);
    //            for (int i = 0; i < numFiles; i++)
    //            {
    //                if (vector[i])
    //                {
    //                    action(i, new long?((long) this.ReadUInt64()));
    //                }
    //                else
    //                {
    //                    long? nullable = null;
    //                    action(i, nullable);
    //                }
    //            }
    //        }
    //    }

    //    private BitVector ReadOptionalBitVector(int length)
    //    {
    //        if (this.ReadByte() != 0)
    //        {
    //            return new BitVector(length, true);
    //        }
    //        return this.ReadBitVector(length);
    //    }

    //    private void ReadPackInfo(out long dataOffset, out List<long> packSizes, out List<uint?> packCRCs)
    //    {
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadPackInfo --");
    //        SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //        try
    //        {
    //            int num2;
    //            BlockType? nullable;
    //            bool flag;
    //            packCRCs = null;
    //            dataOffset = (long) this.ReadNumber();
    //            SharpCompress.Compressor.LZMA.Log.WriteLine("DataOffset: " + ((long) dataOffset));
    //            int capacity = this.ReadNum();
    //            SharpCompress.Compressor.LZMA.Log.WriteLine("NumPackStreams: " + capacity);
    //            this.WaitAttribute(BlockType.Size);
    //            packSizes = new List<long>(capacity);
    //            SharpCompress.Compressor.LZMA.Log.Write("Sizes:");
    //            for (num2 = 0; num2 < capacity; num2++)
    //            {
    //                long item = (long) this.ReadNumber();
    //                SharpCompress.Compressor.LZMA.Log.Write("  " + item);
    //                packSizes.Add(item);
    //            }
    //            SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            goto Label_0117;
    //        Label_00B7:
    //            nullable = this.ReadId();
    //            if (((BlockType) nullable) == BlockType.End)
    //            {
    //                goto Label_011C;
    //            }
    //            if (((BlockType) nullable) == BlockType.CRC)
    //            {
    //                packCRCs = this.ReadHashDigests(capacity);
    //            }
    //            else
    //            {
    //                this.SkipData();
    //            }
    //        Label_0117:
    //            flag = true;
    //            goto Label_00B7;
    //        Label_011C:
    //            if (packCRCs == null)
    //            {
    //                packCRCs = new List<uint?>(capacity);
    //                for (num2 = 0; num2 < capacity; num2++)
    //                {
    //                    uint? nullable3 = null;
    //                    packCRCs.Add(nullable3);
    //                }
    //            }
    //        }
    //        finally
    //        {
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //        }
    //    }

    //    private void ReadStreamsInfo(List<byte[]> dataVector, out long dataOffset, out List<long> packSizes, out List<uint?> packCRCs, out List<CFolder> folders, out List<int> numUnpackStreamsInFolders, out List<long> unpackSizes, out List<uint?> digests)
    //    {
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadStreamsInfo --");
    //        SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //        try
    //        {
    //            BlockType type;
    //            bool flag;
    //            dataOffset = -9223372036854775808L;
    //            packSizes = null;
    //            packCRCs = null;
    //            folders = null;
    //            numUnpackStreamsInFolders = null;
    //            unpackSizes = null;
    //            digests = null;
    //            goto Label_00A2;
    //        Label_003C:
    //            type = this.ReadId().GetValueOrDefault();
    //            if (this.ReadId().HasValue)
    //            {
    //                switch (type)
    //                {
    //                    case BlockType.PackInfo:
    //                        this.ReadPackInfo(out dataOffset, out packSizes, out packCRCs);
    //                        goto Label_00A2;

    //                    case BlockType.UnpackInfo:
    //                        this.ReadUnpackInfo(dataVector, out folders);
    //                        goto Label_00A2;

    //                    case BlockType.SubStreamsInfo:
    //                        this.ReadSubStreamsInfo(folders, out numUnpackStreamsInFolders, out unpackSizes, out digests);
    //                        goto Label_00A2;

    //                    case BlockType.End:
    //                        return;
    //                }
    //            }
    //            throw new InvalidOperationException();
    //        Label_00A2:
    //            flag = true;
    //            goto Label_003C;
    //        }
    //        finally
    //        {
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //        }
    //    }

    //    private void ReadSubStreamsInfo(List<CFolder> folders, out List<int> numUnpackStreamsInFolders, out List<long> unpackSizes, out List<uint?> digests)
    //    {
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadSubStreamsInfo --");
    //        SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //        try
    //        {
    //            BlockType? nullable;
    //            int num;
    //            int num3;
    //            int num5;
    //            bool flag;
    //            numUnpackStreamsInFolders = null;
    //            goto Label_0117;
    //        Label_0020:
    //            nullable = this.ReadId();
    //            BlockType? nullable2 = nullable;
    //            if ((((BlockType) nullable2.GetValueOrDefault()) == BlockType.NumUnpackStream) && nullable2.HasValue)
    //            {
    //                numUnpackStreamsInFolders = new List<int>(folders.Count);
    //                SharpCompress.Compressor.LZMA.Log.Write("NumUnpackStreams:");
    //                for (num = 0; num < folders.Count; num++)
    //                {
    //                    int item = this.ReadNum();
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + item);
    //                    numUnpackStreamsInFolders.Add(item);
    //                }
    //                SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            }
    //            else
    //            {
    //                nullable2 = nullable;
    //                if ((((((BlockType) nullable2.GetValueOrDefault()) == BlockType.CRC) && nullable2.HasValue) || ((((BlockType) (nullable2 = nullable).GetValueOrDefault()) == BlockType.Size) && nullable2.HasValue)) || (((BlockType) nullable) == BlockType.End))
    //                {
    //                    goto Label_011F;
    //                }
    //                this.SkipData();
    //            }
    //        Label_0117:
    //            flag = true;
    //            goto Label_0020;
    //        Label_011F:
    //            if (numUnpackStreamsInFolders == null)
    //            {
    //                numUnpackStreamsInFolders = new List<int>(folders.Count);
    //                for (num = 0; num < folders.Count; num++)
    //                {
    //                    numUnpackStreamsInFolders.Add(1);
    //                }
    //            }
    //            unpackSizes = new List<long>(folders.Count);
    //            for (num = 0; num < numUnpackStreamsInFolders.Count; num++)
    //            {
    //                num3 = numUnpackStreamsInFolders[num];
    //                if (num3 != 0)
    //                {
    //                    SharpCompress.Compressor.LZMA.Log.Write("#{0} StreamSizes:", new object[] { num });
    //                    long num4 = 0L;
    //                    num5 = 1;
    //                    while (num5 < num3)
    //                    {
    //                        if (((BlockType) nullable) == BlockType.Size)
    //                        {
    //                            long num6 = (long) this.ReadNumber();
    //                            SharpCompress.Compressor.LZMA.Log.Write("  " + num6);
    //                            unpackSizes.Add(num6);
    //                            num4 += num6;
    //                        }
    //                        num5++;
    //                    }
    //                    unpackSizes.Add(folders[num].GetUnpackSize() - num4);
    //                    SharpCompress.Compressor.LZMA.Log.WriteLine("  -  rest: " + Enumerable.Last<long>(unpackSizes));
    //                }
    //            }
    //            if (((BlockType) nullable) == BlockType.Size)
    //            {
    //                nullable = this.ReadId();
    //            }
    //            int count = 0;
    //            int capacity = 0;
    //            for (num = 0; num < folders.Count; num++)
    //            {
    //                num3 = numUnpackStreamsInFolders[num];
    //                if (!((num3 == 1) && folders[num].UnpackCRCDefined))
    //                {
    //                    count += num3;
    //                }
    //                capacity += num3;
    //            }
    //            digests = null;
    //            goto Label_0453;
    //        Label_02E7:
    //            nullable2 = nullable;
    //            if ((((BlockType) nullable2.GetValueOrDefault()) == BlockType.CRC) && nullable2.HasValue)
    //            {
    //                digests = new List<uint?>(capacity);
    //                List<uint?> list = this.ReadHashDigests(count);
    //                int num9 = 0;
    //                for (num = 0; num < folders.Count; num++)
    //                {
    //                    num3 = numUnpackStreamsInFolders[num];
    //                    CFolder folder = folders[num];
    //                    if ((num3 == 1) && folder.UnpackCRCDefined)
    //                    {
    //                        digests.Add(new uint?(folder.UnpackCRC.Value));
    //                    }
    //                    else
    //                    {
    //                        num5 = 0;
    //                        while (num5 < num3)
    //                        {
    //                            digests.Add(list[num9]);
    //                            num5++;
    //                            num9++;
    //                        }
    //                    }
    //                }
    //                if ((num9 != count) || (capacity != digests.Count))
    //                {
    //                    Debugger.Break();
    //                }
    //            }
    //            else
    //            {
    //                if (((BlockType) nullable) == BlockType.End)
    //                {
    //                    if (digests == null)
    //                    {
    //                        digests = new List<uint?>(capacity);
    //                        for (num = 0; num < capacity; num++)
    //                        {
    //                            uint? nullable3 = null;
    //                            digests.Add(nullable3);
    //                        }
    //                    }
    //                    return;
    //                }
    //                this.SkipData();
    //            }
    //            nullable = this.ReadId();
    //        Label_0453:
    //            flag = true;
    //            goto Label_02E7;
    //        }
    //        finally
    //        {
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //        }
    //    }

    //    private uint ReadUInt32()
    //    {
    //        return this._currentReader.ReadUInt32();
    //    }

    //    private ulong ReadUInt64()
    //    {
    //        return this._currentReader.ReadUInt64();
    //    }

    //    private void ReadUnpackInfo(List<byte[]> dataVector, out List<CFolder> folders)
    //    {
    //        SharpCompress.Compressor.LZMA.Log.WriteLine("-- ReadUnpackInfo --");
    //        SharpCompress.Compressor.LZMA.Log.PushIndent("  ");
    //        try
    //        {
    //            int num3;
    //            BlockType? nullable;
    //            bool flag;
    //            this.WaitAttribute(BlockType.Folder);
    //            int capacity = this.ReadNum();
    //            SharpCompress.Compressor.LZMA.Log.WriteLine("NumFolders: {0}", new object[] { capacity });
    //            using (CStreamSwitch switch2 = new CStreamSwitch())
    //            {
    //                switch2.Set(this, dataVector);
    //                folders = new List<CFolder>(capacity);
    //                int num2 = 0;
    //                num3 = 0;
    //                while (num3 < capacity)
    //                {
    //                    CFolder folder2 = new CFolder();
    //                    folder2.FirstPackStreamId = num2;
    //                    CFolder item = folder2;
    //                    folders.Add(item);
    //                    this.GetNextFolderItem(item);
    //                    num2 += item.PackStreams.Count;
    //                    num3++;
    //                }
    //            }
    //            this.WaitAttribute(BlockType.CodersUnpackSize);
    //            SharpCompress.Compressor.LZMA.Log.WriteLine("UnpackSizes:");
    //            for (num3 = 0; num3 < capacity; num3++)
    //            {
    //                CFolder folder3 = folders[num3];
    //                SharpCompress.Compressor.LZMA.Log.Write("  #" + num3 + ":");
    //                int numOutStreams = folder3.GetNumOutStreams();
    //                for (int i = 0; i < numOutStreams; i++)
    //                {
    //                    long num6 = (long) this.ReadNumber();
    //                    SharpCompress.Compressor.LZMA.Log.Write("  " + num6);
    //                    folder3.UnpackSizes.Add(num6);
    //                }
    //                SharpCompress.Compressor.LZMA.Log.WriteLine();
    //            }
    //            goto Label_01F9;
    //        Label_016F:
    //            nullable = this.ReadId();
    //            if (((BlockType) nullable) == BlockType.End)
    //            {
    //                return;
    //            }
    //            if (((BlockType) nullable) == BlockType.CRC)
    //            {
    //                List<uint?> list = this.ReadHashDigests(capacity);
    //                for (num3 = 0; num3 < capacity; num3++)
    //                {
    //                    folders[num3].UnpackCRC = list[num3];
    //                }
    //            }
    //            else
    //            {
    //                this.SkipData();
    //            }
    //        Label_01F9:
    //            flag = true;
    //            goto Label_016F;
    //        }
    //        finally
    //        {
    //            SharpCompress.Compressor.LZMA.Log.PopIndent();
    //        }
    //    }

    //    private void SkipData()
    //    {
    //        this._currentReader.SkipData();
    //    }

    //    private void SkipData(long size)
    //    {
    //        this._currentReader.SkipData(size);
    //    }

    //    private DateTime TranslateTime(long time)
    //    {
    //        return DateTime.FromFileTimeUtc(time).ToLocalTime();
    //    }

    //    private DateTime? TranslateTime(long? time)
    //    {
    //        if (time.HasValue)
    //        {
    //            return new DateTime?(this.TranslateTime(time.Value));
    //        }
    //        return null;
    //    }

    //    private void WaitAttribute(BlockType attribute)
    //    {
    //        while (true)
    //        {
    //            BlockType? nullable = this.ReadId();
    //            BlockType? nullable2 = nullable;
    //            BlockType type = attribute;
    //            if ((((BlockType) nullable2.GetValueOrDefault()) == type) && nullable2.HasValue)
    //            {
    //                return;
    //            }
    //            if (((BlockType) nullable) == BlockType.End)
    //            {
    //                throw new InvalidOperationException();
    //            }
    //            this.SkipData();
    //        }
    //    }

    //    internal class CExtractFolderInfo
    //    {
    //        internal List<bool> ExtractStatuses = new List<bool>();
    //        internal int FileIndex;
    //        internal int FolderIndex;

    //        internal CExtractFolderInfo(int fileIndex, int folderIndex)
    //        {
    //            this.FileIndex = fileIndex;
    //            this.FolderIndex = folderIndex;
    //            if (fileIndex != -1)
    //            {
    //                this.ExtractStatuses.Add(true);
    //            }
    //        }
    //    }

    //    private class FolderUnpackStream : Stream
    //    {
    //        private int _currentIndex;
    //        private ArchiveDatabase _db;
    //        private List<bool> _extractStatuses;
    //        private int _otherIndex;
    //        private long _rem;
    //        private int _startIndex;
    //        private Stream _stream;

    //        public FolderUnpackStream(ArchiveDatabase db, int p, int startIndex, List<bool> list)
    //        {
    //            this._db = db;
    //            this._otherIndex = p;
    //            this._startIndex = startIndex;
    //            this._extractStatuses = list;
    //        }

    //        public override void Flush()
    //        {
    //            throw new NotSupportedException();
    //        }

    //        private void OpenFile()
    //        {
    //            bool flag = !this._extractStatuses[this._currentIndex];
    //            int num = this._startIndex + this._currentIndex;
    //            int num2 = this._otherIndex + num;
    //            SharpCompress.Compressor.LZMA.Log.WriteLine(this._db.Files[num].Name);
    //            if (this._db.Files[num].CrcDefined)
    //            {
    //                this._stream = new CrcCheckStream(this._db.Files[num].Crc.Value);
    //            }
    //            else
    //            {
    //                this._stream = new MemoryStream();
    //            }
    //            this._rem = this._db.Files[num].Size;
    //        }

    //        private void ProcessEmptyFiles()
    //        {
    //            while ((this._currentIndex < this._extractStatuses.Count) && (this._db.Files[this._startIndex + this._currentIndex].Size == 0L))
    //            {
    //                this.OpenFile();
    //                this._stream.Dispose();
    //                this._stream = null;
    //                this._currentIndex++;
    //            }
    //        }

    //        public override int Read(byte[] buffer, int offset, int count)
    //        {
    //            throw new NotSupportedException();
    //        }

    //        public override long Seek(long offset, SeekOrigin origin)
    //        {
    //            throw new NotSupportedException();
    //        }

    //        public override void SetLength(long value)
    //        {
    //            throw new NotSupportedException();
    //        }

    //        public override void Write(byte[] buffer, int offset, int count)
    //        {
    //            while (count != 0)
    //            {
    //                if (this._stream != null)
    //                {
    //                    int num = count;
    //                    if (num > this._rem)
    //                    {
    //                        num = (int) this._rem;
    //                    }
    //                    this._stream.Write(buffer, offset, num);
    //                    count -= num;
    //                    this._rem -= num;
    //                    offset += num;
    //                    if (this._rem == 0L)
    //                    {
    //                        this._stream.Dispose();
    //                        this._stream = null;
    //                        this._currentIndex++;
    //                        this.ProcessEmptyFiles();
    //                    }
    //                }
    //                else
    //                {
    //                    this.ProcessEmptyFiles();
    //                    if (this._currentIndex == this._extractStatuses.Count)
    //                    {
    //                        Debugger.Break();
    //                        throw new NotSupportedException();
    //                    }
    //                    this.OpenFile();
    //                }
    //            }
    //        }

    //        public override bool CanRead
    //        {
    //            get
    //            {
    //                return true;
    //            }
    //        }

    //        public override bool CanSeek
    //        {
    //            get
    //            {
    //                return false;
    //            }
    //        }

    //        public override bool CanWrite
    //        {
    //            get
    //            {
    //                return false;
    //            }
    //        }

    //        public override long Length
    //        {
    //            get
    //            {
    //                throw new NotSupportedException();
    //            }
    //        }

    //        public override long Position
    //        {
    //            get
    //            {
    //                throw new NotSupportedException();
    //            }
    //            set
    //            {
    //                throw new NotSupportedException();
    //            }
    //        }
    //    }
    //}

    internal class ArchiveReader {
        internal Stream _stream;
        internal Stack<DataReader> _readerStack = new Stack<DataReader>();
        internal DataReader _currentReader;
        internal long _streamOrigin;
        internal long _streamEnding;
        internal byte[] _header;

        private Dictionary<int, Stream> _cachedStreams = new Dictionary<int, Stream>();

        internal void AddByteStream(byte[] buffer, int offset, int length) {
            _readerStack.Push(_currentReader);
            _currentReader = new DataReader(buffer, offset, length);
        }

        internal void DeleteByteStream() {
            _currentReader = _readerStack.Pop();
        }

        #region Private Methods - Data Reader

        internal Byte ReadByte() {
            return _currentReader.ReadByte();
        }

        private void ReadBytes(byte[] buffer, int offset, int length) {
            _currentReader.ReadBytes(buffer, offset, length);
        }

        private ulong ReadNumber() {
            return _currentReader.ReadNumber();
        }

        internal int ReadNum() {
            return _currentReader.ReadNum();
        }

        private uint ReadUInt32() {
            return _currentReader.ReadUInt32();
        }

        private ulong ReadUInt64() {
            return _currentReader.ReadUInt64();
        }

        private BlockType? ReadId() {
            ulong id = _currentReader.ReadNumber();
            if (id > 25)
                return null;
#if DEBUG
            Log.WriteLine("ReadId: {0}", (BlockType)id);
#endif
            return (BlockType)id;
        }

        private void SkipData(long size) {
            _currentReader.SkipData(size);
        }

        private void SkipData() {
            _currentReader.SkipData();
        }

        private void WaitAttribute(BlockType attribute) {
            for (; ; ) {
                BlockType? type = ReadId();
                if (type == attribute)
                    return;
                if (type == BlockType.End)
                    throw new InvalidOperationException();
                SkipData();
            }
        }

        private void ReadArchiveProperties() {
            while (ReadId() != BlockType.End)
                SkipData();
        }

        #endregion

        #region Private Methods - Reader Utilities

        private BitVector ReadBitVector(int length) {
            var bits = new BitVector(length);

            byte data = 0;
            byte mask = 0;

            for (int i = 0; i < length; i++) {
                if (mask == 0) {
                    data = ReadByte();
                    mask = 0x80;
                }

                if ((data & mask) != 0)
                    bits.SetBit(i);

                mask >>= 1;
            }

            return bits;
        }

        private BitVector ReadOptionalBitVector(int length) {
            byte allTrue = ReadByte();
            if (allTrue != 0)
                return new BitVector(length, true);

            return ReadBitVector(length);
        }

        private void ReadNumberVector(List<byte[]> dataVector, int numFiles, Action<int, long?> action) {
            var defined = ReadOptionalBitVector(numFiles);

            using (CStreamSwitch streamSwitch = new CStreamSwitch()) {
                streamSwitch.Set(this, dataVector);

                for (int i = 0; i < numFiles; i++) {
                    if (defined[i])
                        action(i, checked((long)ReadUInt64()));
                    else
                        action(i, null);
                }
            }
        }

        private DateTime TranslateTime(long time) {
            // FILETIME = 100-nanosecond intervals since January 1, 1601 (UTC)
            return DateTime.FromFileTimeUtc(time).ToLocalTime();
        }

        private DateTime? TranslateTime(long? time) {
            if (time.HasValue)
                return TranslateTime(time.Value);
            else
                return null;
        }

        private void ReadDateTimeVector(List<byte[]> dataVector, int numFiles, Action<int, DateTime?> action) {
            ReadNumberVector(dataVector, numFiles, (index, value) => action(index, TranslateTime(value)));
        }

        private void ReadAttributeVector(List<byte[]> dataVector, int numFiles, Action<int, uint?> action) {
            BitVector boolVector = ReadOptionalBitVector(numFiles);
            using (var streamSwitch = new CStreamSwitch()) {
                streamSwitch.Set(this, dataVector);
                for (int i = 0; i < numFiles; i++) {
                    if (boolVector[i])
                        action(i, ReadUInt32());
                    else
                        action(i, null);
                }
            }
        }

        #endregion

        #region Private Methods

        private void GetNextFolderItem(CFolder folder) {
#if DEBUG
            Log.WriteLine("-- GetNextFolderItem --");
            Log.PushIndent();
#endif
            try {
                int numCoders = ReadNum();
#if DEBUG
                Log.WriteLine("NumCoders: " + numCoders);
#endif
                folder.Coders = new List<CCoderInfo>(numCoders);
                int numInStreams = 0;
                int numOutStreams = 0;
                for (int i = 0; i < numCoders; i++) {
#if DEBUG
                    Log.WriteLine("-- Coder --");
                    Log.PushIndent();
#endif
                    try {
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

                        if ((mainByte & 0x10) != 0) {
                            coder.NumInStreams = ReadNum();
                            coder.NumOutStreams = ReadNum();
#if DEBUG
                            Log.WriteLine("Complex Stream (In: " + coder.NumInStreams + " - Out: " + coder.NumOutStreams + ")");
#endif
                        }
                        else {
#if DEBUG
                            Log.WriteLine("Simple Stream (In: 1 - Out: 1)");
#endif
                            coder.NumInStreams = 1;
                            coder.NumOutStreams = 1;
                        }

                        if ((mainByte & 0x20) != 0) {
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
                    finally {
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
                for (int i = 0; i < numBindPairs; i++) {
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
                if (numPackStreams == 1) {
                    for (int i = 0; i < numInStreams; i++) {
                        if (folder.FindBindPairForInStream(i) < 0) {
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
                else {
#if DEBUG
                    Log.WriteLine("Multiple PackStreams ...");
                    Log.PushIndent();
#endif
                    for (int i = 0; i < numPackStreams; i++) {
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
            finally {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private List<uint?> ReadHashDigests(int count) {
#if DEBUG
            Log.Write("ReadHashDigests:");
#endif

            var defined = ReadOptionalBitVector(count);
            var digests = new List<uint?>(count);
            for (int i = 0; i < count; i++) {
                if (defined[i]) {
                    uint crc = ReadUInt32();
#if DEBUG
                    Log.Write("  " + crc.ToString("x8"));
#endif
                    digests.Add(crc);
                }
                else {
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

        private void ReadPackInfo(out long dataOffset, out List<long> packSizes, out List<uint?> packCRCs) {
#if DEBUG
            Log.WriteLine("-- ReadPackInfo --");
            Log.PushIndent();
#endif
            try {
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
                for (int i = 0; i < numPackStreams; i++) {
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
                for (; ; ) {
                    type = ReadId();
                    if (type == BlockType.End)
                        break;
                    if (type == BlockType.CRC) {
                        packCRCs = ReadHashDigests(numPackStreams);
                        continue;
                    }
                    SkipData();
                }

                if (packCRCs == null) {
                    packCRCs = new List<uint?>(numPackStreams);
                    for (int i = 0; i < numPackStreams; i++)
                        packCRCs.Add(null);
                }
            }
            finally {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private void ReadUnpackInfo(List<byte[]> dataVector, out List<CFolder> folders) {
#if DEBUG
            Log.WriteLine("-- ReadUnpackInfo --");
            Log.PushIndent();
#endif
            try {
                WaitAttribute(BlockType.Folder);
                int numFolders = ReadNum();
#if DEBUG
                Log.WriteLine("NumFolders: {0}", numFolders);
#endif

                using (CStreamSwitch streamSwitch = new CStreamSwitch()) {
                    streamSwitch.Set(this, dataVector);
                    //folders.Clear();
                    //folders.Reserve(numFolders);
                    folders = new List<CFolder>(numFolders);
                    int index = 0;
                    for (int i = 0; i < numFolders; i++) {
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
                for (int i = 0; i < numFolders; i++) {
                    CFolder folder = folders[i];
#if DEBUG
                    Log.Write("  #" + i + ":");
#endif
                    int numOutStreams = folder.GetNumOutStreams();
                    for (int j = 0; j < numOutStreams; j++) {
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

                for (; ; ) {
                    BlockType? type = ReadId();
                    if (type == BlockType.End)
                        return;

                    if (type == BlockType.CRC) {
                        List<uint?> crcs = ReadHashDigests(numFolders);
                        for (int i = 0; i < numFolders; i++)
                            folders[i].UnpackCRC = crcs[i];
                        continue;
                    }

                    SkipData();
                }
            }
            finally {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private void ReadSubStreamsInfo(List<CFolder> folders, out List<int> numUnpackStreamsInFolders,
                                        out List<long> unpackSizes, out List<uint?> digests) {
#if DEBUG
            Log.WriteLine("-- ReadSubStreamsInfo --");
            Log.PushIndent();
#endif
            try {
                numUnpackStreamsInFolders = null;

                BlockType? type;
                for (; ; ) {
                    type = ReadId();
                    if (type == BlockType.NumUnpackStream) {
                        numUnpackStreamsInFolders = new List<int>(folders.Count);
#if DEBUG
                        Log.Write("NumUnpackStreams:");
#endif
                        for (int i = 0; i < folders.Count; i++) {
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

                if (numUnpackStreamsInFolders == null) {
                    numUnpackStreamsInFolders = new List<int>(folders.Count);
                    for (int i = 0; i < folders.Count; i++)
                        numUnpackStreamsInFolders.Add(1);
                }

                unpackSizes = new List<long>(folders.Count);
                for (int i = 0; i < numUnpackStreamsInFolders.Count; i++) {
                    // v3.13 incorrectly worked with empty folders
                    // v4.07: we check that folder is empty
                    int numSubstreams = numUnpackStreamsInFolders[i];
                    if (numSubstreams == 0)
                        continue;
#if DEBUG
                    Log.Write("#{0} StreamSizes:", i);
#endif
                    long sum = 0;
                    for (int j = 1; j < numSubstreams; j++) {
                        if (type == BlockType.Size) {
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
                for (int i = 0; i < folders.Count; i++) {
                    int numSubstreams = numUnpackStreamsInFolders[i];
                    if (numSubstreams != 1 || !folders[i].UnpackCRCDefined)
                        numDigests += numSubstreams;
                    numDigestsTotal += numSubstreams;
                }

                digests = null;

                for (; ; ) {
                    if (type == BlockType.CRC) {
                        digests = new List<uint?>(numDigestsTotal);

                        List<uint?> digests2 = ReadHashDigests(numDigests);

                        int digestIndex = 0;
                        for (int i = 0; i < folders.Count; i++) {
                            int numSubstreams = numUnpackStreamsInFolders[i];
                            CFolder folder = folders[i];
                            if (numSubstreams == 1 && folder.UnpackCRCDefined) {
                                digests.Add(folder.UnpackCRC.Value);
                            }
                            else {
                                for (int j = 0; j < numSubstreams; j++, digestIndex++)
                                    digests.Add(digests2[digestIndex]);
                            }
                        }

                        if (digestIndex != numDigests || numDigestsTotal != digests.Count)
                            System.Diagnostics.Debugger.Break();
                    }
                    else if (type == BlockType.End) {
                        if (digests == null) {
                            digests = new List<uint?>(numDigestsTotal);
                            for (int i = 0; i < numDigestsTotal; i++)
                                digests.Add(null);
                        }
                        return;
                    }
                    else {
                        SkipData();
                    }

                    type = ReadId();
                }
            }
            finally {
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
            out List<uint?> digests) {
#if DEBUG
            Log.WriteLine("-- ReadStreamsInfo --");
            Log.PushIndent();
#endif
            try {
                dataOffset = long.MinValue;
                packSizes = null;
                packCRCs = null;
                folders = null;
                numUnpackStreamsInFolders = null;
                unpackSizes = null;
                digests = null;

                for (; ; ) {
                    switch (ReadId()) {
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
            finally {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private List<byte[]> ReadAndDecodePackedStreams(long baseOffset, IPasswordProvider pass) {
#if DEBUG
            Log.WriteLine("-- ReadAndDecodePackedStreams --");
            Log.PushIndent();
#endif
            try {
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
                foreach (var folder in folders) {
                    long oldDataStartPos = dataStartPos;
                    long[] myPackSizes = new long[folder.PackStreams.Count];
                    for (int i = 0; i < myPackSizes.Length; i++) {
                        long packSize = packSizes[packIndex + i];
                        myPackSizes[i] = packSize;
                        dataStartPos += packSize;
                    }

                    var outStream = DecoderStreamHelper.CreateDecoderStream(_stream, oldDataStartPos, myPackSizes,
                                                                            folder, pass);

                    int unpackSize = checked((int)folder.GetUnpackSize());
                    byte[] data = new byte[unpackSize];
                    //outStream.ReadExact(data, 0, data.Length);
                    Utils.ReadExact(outStream,data, 0, data.Length);
                    if (outStream.ReadByte() >= 0)
                        throw new InvalidOperationException("Decoded stream is longer than expected.");
                    dataVector.Add(data);

                    if (folder.UnpackCRCDefined)
                        if (CRC.Finish(CRC.Update(CRC.kInitCRC, data, 0, unpackSize)) != folder.UnpackCRC)
                            throw new InvalidOperationException("Decoded stream does not match expected CRC.");
                }
                return dataVector;
            }
            finally {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        private void ReadHeader(ArchiveDatabase db, IPasswordProvider getTextPassword) {
#if DEBUG
            Log.WriteLine("-- ReadHeader --");
            Log.PushIndent();
#endif
            try {
                BlockType? type = ReadId();

                if (type == BlockType.ArchiveProperties) {
                    ReadArchiveProperties();
                    type = ReadId();
                }

                List<byte[]> dataVector = null;
                if (type == BlockType.AdditionalStreamsInfo) {
                    dataVector = ReadAndDecodePackedStreams(db.StartPositionAfterHeader, getTextPassword);
                    type = ReadId();
                }

                List<long> unpackSizes;
                List<uint?> digests;

                if (type == BlockType.MainStreamsInfo) {
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
                else {
                    unpackSizes = new List<long>(db.Folders.Count);
                    digests = new List<uint?>(db.Folders.Count);
                    db.NumUnpackStreamsVector = new List<int>(db.Folders.Count);
                    for (int i = 0; i < db.Folders.Count; i++) {
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

                for (; ; ) {
                    type = ReadId();
                    if (type == BlockType.End)
                        break;

                    long size = checked((long)ReadNumber()); // TODO: throw invalid data on negative
                    int oldPos = _currentReader.Offset;
                    switch (type) {
                        case BlockType.Name:
                            using (var streamSwitch = new CStreamSwitch()) {
                                streamSwitch.Set(this, dataVector);
#if DEBUG
                                Log.Write("FileNames:");
#endif
                                for (int i = 0; i < db.Files.Count; i++) {
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
                            ReadAttributeVector(dataVector, numFiles, delegate(int i, uint? attr) {
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
                            for (int i = 0; i < emptyStreamVector.Length; i++) {
                                if (emptyStreamVector[i]) {
#if DEBUG
                                    Log.Write("x");
#endif
                                    numEmptyStreams++;
                                }
                                else {
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
                            ReadNumberVector(dataVector, numFiles, delegate(int i, long? startPos) {
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
                            ReadDateTimeVector(dataVector, numFiles, delegate(int i, DateTime? time) {
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
                            ReadDateTimeVector(dataVector, numFiles, delegate(int i, DateTime? time) {
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
                            ReadDateTimeVector(dataVector, numFiles, delegate(int i, DateTime? time) {
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
                for (int i = 0; i < numFiles; i++) {
                    CFileItem file = db.Files[i];
                    file.HasStream = !emptyStreamVector[i];
                    if (file.HasStream) {
                        file.IsDir = false;
                        file.IsAnti = false;
                        file.Size = unpackSizes[sizeIndex];
                        file.Crc = digests[sizeIndex];
                        sizeIndex++;
                    }
                    else {
                        file.IsDir = !emptyFileVector[emptyFileIndex];
                        file.IsAnti = antiFileVector[emptyFileIndex];
                        emptyFileIndex++;
                        file.Size = 0;
                        file.Crc = null;
                    }
                }
            }
            finally {
#if DEBUG
                Log.PopIndent();
#endif
            }
        }

        #endregion

        #region Public Methods

        public void Open(Stream stream) {
            Close();

            _streamOrigin = stream.Position;
            _streamEnding = stream.Length;

            // TODO: Check Signature!
            _header = new byte[0x20];
            for (int offset = 0; offset < 0x20; ) {
                int delta = stream.Read(_header, offset, 0x20 - offset);
                if (delta == 0)
                    throw new EndOfStreamException();
                offset += delta;
            }

            _stream = stream;
        }

        public void Close() {
            if (_stream != null)
                _stream.Dispose();

            foreach (var stream in _cachedStreams.Values)
                stream.Dispose();

            _cachedStreams.Clear();
        }

        public ArchiveDatabase ReadDatabase(IPasswordProvider pass) {
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
            if (nextHeaderSize == 0) {
                db.Fill();
                return db;
            }


            if (nextHeaderOffset < 0 || nextHeaderSize < 0 || nextHeaderSize > Int32.MaxValue)
                throw new InvalidOperationException();

            if (nextHeaderOffset > _streamEnding - db.StartPositionAfterHeader)
                throw new IndexOutOfRangeException();

            _stream.Seek(nextHeaderOffset, SeekOrigin.Current);

            byte[] header = new byte[nextHeaderSize];
            // _stream.ReadExact(header, 0, header.Length);
            Utils.ReadExact(_stream,header,0,header.Length);
            if (CRC.Finish(CRC.Update(CRC.kInitCRC, header, 0, header.Length)) != nextHeaderCrc)
                throw new InvalidOperationException();

            using (CStreamSwitch streamSwitch = new CStreamSwitch()) {
                streamSwitch.Set(this, header);

                BlockType? type = ReadId();
                if (type != BlockType.Header) {
                    if (type != BlockType.EncodedHeader)
                        throw new InvalidOperationException();

                    var dataVector = ReadAndDecodePackedStreams(db.StartPositionAfterHeader, pass);

                    // compressed header without content is odd but ok
                    if (dataVector.Count == 0) {
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

        internal class CExtractFolderInfo {
            internal int FileIndex;
            internal int FolderIndex;
            internal List<bool> ExtractStatuses = new List<bool>();

            internal CExtractFolderInfo(int fileIndex, int folderIndex) {
                FileIndex = fileIndex;
                FolderIndex = folderIndex;
                if (fileIndex != -1)
                    ExtractStatuses.Add(true);
            }
        }

        private class FolderUnpackStream : Stream {
            private ArchiveDatabase _db;
            private int _otherIndex;
            private int _startIndex;
            private List<bool> _extractStatuses;

            public FolderUnpackStream(ArchiveDatabase db, int p, int startIndex, List<bool> list) {
                this._db = db;
                this._otherIndex = p;
                this._startIndex = startIndex;
                this._extractStatuses = list;
            }

            #region Stream

            public override bool CanRead {
                get { return true; }
            }

            public override bool CanSeek {
                get { return false; }
            }

            public override bool CanWrite {
                get { return false; }
            }

            public override void Flush() {
                throw new NotSupportedException();
            }

            public override long Length {
                get { throw new NotSupportedException(); }
            }

            public override long Position {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }

            public override int Read(byte[] buffer, int offset, int count) {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin) {
                throw new NotSupportedException();
            }

            public override void SetLength(long value) {
                throw new NotSupportedException();
            }

            private Stream _stream;
            private long _rem;
            private int _currentIndex;

            private void ProcessEmptyFiles() {
                while (_currentIndex < _extractStatuses.Count && _db.Files[_startIndex + _currentIndex].Size == 0) {
                    OpenFile();
                    _stream.Dispose();
                    _stream = null;
                    _currentIndex++;
                }
            }

            private void OpenFile() {
                bool skip = !_extractStatuses[_currentIndex];
                int index = _startIndex + _currentIndex;
                int realIndex = _otherIndex + index;
                //string filename = @"D:\_testdump\" + _db.Files[index].Name;
                //Directory.CreateDirectory(Path.GetDirectoryName(filename));
                //_stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Delete);
#if DEBUG
                Log.WriteLine(_db.Files[index].Name);
#endif
                if (_db.Files[index].CrcDefined)
                    _stream = new CrcCheckStream(_db.Files[index].Crc.Value);
                else
                    _stream = new MemoryStream();
                _rem = _db.Files[index].Size;
            }

            public override void Write(byte[] buffer, int offset, int count) {
                while (count != 0) {
                    if (_stream != null) {
                        int write = count;
                        if (write > _rem)
                            write = (int)_rem;
                        _stream.Write(buffer, offset, write);
                        count -= write;
                        _rem -= write;
                        offset += write;
                        if (_rem == 0) {
                            _stream.Dispose();
                            _stream = null;
                            _currentIndex++;
                            ProcessEmptyFiles();
                        }
                    }
                    else {
                        ProcessEmptyFiles();
                        if (_currentIndex == _extractStatuses.Count) {
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

        private Stream GetCachedDecoderStream(ArchiveDatabase _db, int folderIndex, IPasswordProvider pw) {
            Stream s;
            if (!_cachedStreams.TryGetValue(folderIndex, out s)) {
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

        public Stream OpenStream(ArchiveDatabase _db, int fileIndex, IPasswordProvider pw) {
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

        public void Extract(ArchiveDatabase _db, int[] indices, IPasswordProvider pw) {
            int numItems;
            bool allFilesMode = (indices == null);
            if (allFilesMode)
                numItems = _db.Files.Count;
            else
                numItems = indices.Length;

            if (numItems == 0)
                return;

            List<CExtractFolderInfo> extractFolderInfoVector = new List<CExtractFolderInfo>();
            for (int i = 0; i < numItems; i++) {
                int fileIndex = allFilesMode ? i : indices[i];

                int folderIndex = _db.FileIndexToFolderIndexMap[fileIndex];
                if (folderIndex == -1) {
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

            foreach (CExtractFolderInfo efi in extractFolderInfoVector) {
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
                for (; ; ) {
                    int processed = s.Read(buffer, 0, buffer.Length);
                    if (processed == 0)
                        break;
                    outStream.Write(buffer, 0, processed);
                }
            }
        }

        public IEnumerable<CFileItem> GetFiles(ArchiveDatabase db) {
            return db.Files;
        }

        public int GetFileIndex(ArchiveDatabase db, CFileItem item) {
            return db.Files.IndexOf(item);
        }

        #endregion
    }
}

