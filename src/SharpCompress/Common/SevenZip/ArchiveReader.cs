#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilites;
using SharpCompress.IO;

namespace SharpCompress.Common.SevenZip;

internal class ArchiveReader
{
    internal Stream _stream;
    internal Stack<DataReader> _readerStack = new();
    internal DataReader _currentReader;
    internal long _streamOrigin;
    internal long _streamEnding;
    internal byte[] _header;

    private readonly Dictionary<int, Stream> _cachedStreams = new();

    internal void AddByteStream(byte[] buffer, int offset, int length)
    {
        _readerStack.Push(_currentReader);
        _currentReader = new DataReader(buffer, offset, length);
    }

    internal void DeleteByteStream() => _currentReader = _readerStack.Pop();

    #region Private Methods - Data Reader

    internal byte ReadByte() => _currentReader.ReadByte();

    private void ReadBytes(byte[] buffer, int offset, int length) =>
        _currentReader.ReadBytes(buffer, offset, length);

    private ulong ReadNumber() => _currentReader.ReadNumber();

    internal int ReadNum() => _currentReader.ReadNum();

    private uint ReadUInt32() => _currentReader.ReadUInt32();

    private ulong ReadUInt64() => _currentReader.ReadUInt64();

    private BlockType? ReadId()
    {
        var id = _currentReader.ReadNumber();
        if (id > 25)
        {
            return null;
        }
#if DEBUG
        Log.WriteLine("ReadId: {0}", (BlockType)id);
#endif
        return (BlockType)id;
    }

    private void SkipData(long size) => _currentReader.SkipData(size);

    private void SkipData() => _currentReader.SkipData();

    private void WaitAttribute(BlockType attribute)
    {
        for (; ; )
        {
            var type = ReadId();
            if (type == attribute)
            {
                return;
            }
            if (type == BlockType.End)
            {
                throw new InvalidOperationException();
            }
            SkipData();
        }
    }

    private void ReadArchiveProperties()
    {
        while (ReadId() != BlockType.End)
        {
            SkipData();
        }
    }

    #endregion

    #region Private Methods - Reader Utilities

    private BitVector ReadBitVector(int length)
    {
        var bits = new BitVector(length);

        byte data = 0;
        byte mask = 0;

        for (var i = 0; i < length; i++)
        {
            if (mask == 0)
            {
                data = ReadByte();
                mask = 0x80;
            }

            if ((data & mask) != 0)
            {
                bits.SetBit(i);
            }

            mask >>= 1;
        }

        return bits;
    }

    private BitVector ReadOptionalBitVector(int length)
    {
        var allTrue = ReadByte();
        if (allTrue != 0)
        {
            return new BitVector(length, true);
        }

        return ReadBitVector(length);
    }

    private void ReadNumberVector(List<byte[]> dataVector, int numFiles, Action<int, long?> action)
    {
        var defined = ReadOptionalBitVector(numFiles);

        using var streamSwitch = new CStreamSwitch();
        streamSwitch.Set(this, dataVector);

        for (var i = 0; i < numFiles; i++)
        {
            if (defined[i])
            {
                action(i, checked((long)ReadUInt64()));
            }
            else
            {
                action(i, null);
            }
        }
    }

    private DateTime TranslateTime(long time) =>
        // FILETIME = 100-nanosecond intervals since January 1, 1601 (UTC)
        DateTime.FromFileTimeUtc(time).ToLocalTime();

    private DateTime? TranslateTime(long? time)
    {
        if (time.HasValue && time.Value >= 0 && time.Value <= 2650467743999999999) //maximum Windows file time 31.12.9999
        {
            return TranslateTime(time.Value);
        }
        return null;
    }

    private void ReadDateTimeVector(
        List<byte[]> dataVector,
        int numFiles,
        Action<int, DateTime?> action
    ) =>
        ReadNumberVector(
            dataVector,
            numFiles,
            (index, value) => action(index, TranslateTime(value))
        );

    private void ReadAttributeVector(
        List<byte[]> dataVector,
        int numFiles,
        Action<int, uint?> action
    )
    {
        var boolVector = ReadOptionalBitVector(numFiles);
        using var streamSwitch = new CStreamSwitch();
        streamSwitch.Set(this, dataVector);
        for (var i = 0; i < numFiles; i++)
        {
            if (boolVector[i])
            {
                action(i, ReadUInt32());
            }
            else
            {
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
            var numCoders = ReadNum();
#if DEBUG
            Log.WriteLine("NumCoders: " + numCoders);
#endif
            folder._coders = new List<CCoderInfo>(numCoders);
            var numInStreams = 0;
            var numOutStreams = 0;
            for (var i = 0; i < numCoders; i++)
            {
#if DEBUG
                Log.WriteLine("-- Coder --");
                Log.PushIndent();
#endif
                try
                {
                    var coder = new CCoderInfo();
                    folder._coders.Add(coder);

                    var mainByte = ReadByte();
                    var idSize = (mainByte & 0xF);
                    var longId = new byte[idSize];
                    ReadBytes(longId, 0, idSize);
#if DEBUG
                    Log.WriteLine(
                        "MethodId: "
                            + string.Join(
                                "",
                                Enumerable
                                    .Range(0, idSize)
                                    .Select(x => longId[x].ToString("x2"))
                                    .ToArray()
                            )
                    );
#endif
                    if (idSize > 8)
                    {
                        throw new NotSupportedException();
                    }
                    ulong id = 0;
                    for (var j = 0; j < idSize; j++)
                    {
                        id |= (ulong)longId[idSize - 1 - j] << (8 * j);
                    }
                    coder._methodId = new CMethodId(id);

                    if ((mainByte & 0x10) != 0)
                    {
                        coder._numInStreams = ReadNum();
                        coder._numOutStreams = ReadNum();
#if DEBUG
                        Log.WriteLine(
                            "Complex Stream (In: "
                                + coder._numInStreams
                                + " - Out: "
                                + coder._numOutStreams
                                + ")"
                        );
#endif
                    }
                    else
                    {
#if DEBUG
                        Log.WriteLine("Simple Stream (In: 1 - Out: 1)");
#endif
                        coder._numInStreams = 1;
                        coder._numOutStreams = 1;
                    }

                    if ((mainByte & 0x20) != 0)
                    {
                        var propsSize = ReadNum();
                        coder._props = new byte[propsSize];
                        ReadBytes(coder._props, 0, propsSize);
#if DEBUG
                        Log.WriteLine(
                            "Settings: "
                                + string.Join(
                                    "",
                                    coder._props.Select(bt => bt.ToString("x2")).ToArray()
                                )
                        );
#endif
                    }

                    if ((mainByte & 0x80) != 0)
                    {
                        throw new NotSupportedException();
                    }

                    numInStreams += coder._numInStreams;
                    numOutStreams += coder._numOutStreams;
                }
                finally
                {
#if DEBUG
                    Log.PopIndent();
#endif
                }
            }

            var numBindPairs = numOutStreams - 1;
            folder._bindPairs = new List<CBindPair>(numBindPairs);
#if DEBUG
            Log.WriteLine("BindPairs: " + numBindPairs);
            Log.PushIndent();
#endif
            for (var i = 0; i < numBindPairs; i++)
            {
                var bp = new CBindPair();
                bp._inIndex = ReadNum();
                bp._outIndex = ReadNum();
                folder._bindPairs.Add(bp);
#if DEBUG
                Log.WriteLine("#" + i + " - In: " + bp._inIndex + " - Out: " + bp._outIndex);
#endif
            }
#if DEBUG
            Log.PopIndent();
#endif

            if (numInStreams < numBindPairs)
            {
                throw new NotSupportedException();
            }

            var numPackStreams = numInStreams - numBindPairs;

            //folder.PackStreams.Reserve(numPackStreams);
            if (numPackStreams == 1)
            {
                for (var i = 0; i < numInStreams; i++)
                {
                    if (folder.FindBindPairForInStream(i) < 0)
                    {
#if DEBUG
                        Log.WriteLine("Single PackStream: #" + i);
#endif
                        folder._packStreams.Add(i);
                        break;
                    }
                }

                if (folder._packStreams.Count != 1)
                {
                    throw new NotSupportedException();
                }
            }
            else
            {
#if DEBUG
                Log.WriteLine("Multiple PackStreams ...");
                Log.PushIndent();
#endif
                for (var i = 0; i < numPackStreams; i++)
                {
                    var num = ReadNum();
#if DEBUG
                    Log.WriteLine("#" + i + " - " + num);
#endif
                    folder._packStreams.Add(num);
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
        for (var i = 0; i < count; i++)
        {
            if (defined[i])
            {
                var crc = ReadUInt32();
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

    private void ReadPackInfo(
        out long dataOffset,
        out List<long> packSizes,
        out List<uint?> packCrCs
    )
    {
#if DEBUG
        Log.WriteLine("-- ReadPackInfo --");
        Log.PushIndent();
#endif
        try
        {
            packCrCs = null;

            dataOffset = checked((long)ReadNumber());
#if DEBUG
            Log.WriteLine("DataOffset: " + dataOffset);
#endif

            var numPackStreams = ReadNum();
#if DEBUG
            Log.WriteLine("NumPackStreams: " + numPackStreams);
#endif

            WaitAttribute(BlockType.Size);
            packSizes = new List<long>(numPackStreams);
#if DEBUG
            Log.Write("Sizes:");
#endif
            for (var i = 0; i < numPackStreams; i++)
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
                {
                    break;
                }
                if (type == BlockType.Crc)
                {
                    packCrCs = ReadHashDigests(numPackStreams);
                    continue;
                }
                SkipData();
            }

            if (packCrCs is null)
            {
                packCrCs = new List<uint?>(numPackStreams);
                for (var i = 0; i < numPackStreams; i++)
                {
                    packCrCs.Add(null);
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

    private void ReadUnpackInfo(List<byte[]> dataVector, out List<CFolder> folders)
    {
#if DEBUG
        Log.WriteLine("-- ReadUnpackInfo --");
        Log.PushIndent();
#endif
        try
        {
            WaitAttribute(BlockType.Folder);
            var numFolders = ReadNum();
#if DEBUG
            Log.WriteLine("NumFolders: {0}", numFolders);
#endif

            using (var streamSwitch = new CStreamSwitch())
            {
                streamSwitch.Set(this, dataVector);

                //folders.Clear();
                //folders.Reserve(numFolders);
                folders = new List<CFolder>(numFolders);
                var index = 0;
                for (var i = 0; i < numFolders; i++)
                {
                    var f = new CFolder { _firstPackStreamId = index };
                    folders.Add(f);
                    GetNextFolderItem(f);
                    index += f._packStreams.Count;
                }
            }

            WaitAttribute(BlockType.CodersUnpackSize);
#if DEBUG
            Log.WriteLine("UnpackSizes:");
#endif
            for (var i = 0; i < numFolders; i++)
            {
                var folder = folders[i];
#if DEBUG
                Log.Write("  #" + i + ":");
#endif
                var numOutStreams = folder.GetNumOutStreams();
                for (var j = 0; j < numOutStreams; j++)
                {
                    var size = checked((long)ReadNumber());
#if DEBUG
                    Log.Write("  " + size);
#endif
                    folder._unpackSizes.Add(size);
                }
#if DEBUG
                Log.WriteLine();
#endif
            }

            for (; ; )
            {
                var type = ReadId();
                if (type == BlockType.End)
                {
                    return;
                }

                if (type == BlockType.Crc)
                {
                    var crcs = ReadHashDigests(numFolders);
                    for (var i = 0; i < numFolders; i++)
                    {
                        folders[i]._unpackCrc = crcs[i];
                    }
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

    private void ReadSubStreamsInfo(
        List<CFolder> folders,
        out List<int> numUnpackStreamsInFolders,
        out List<long> unpackSizes,
        out List<uint?> digests
    )
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
                    for (var i = 0; i < folders.Count; i++)
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
                if (type is BlockType.Crc or BlockType.Size)
                {
                    break;
                }
                if (type == BlockType.End)
                {
                    break;
                }
                SkipData();
            }

            if (numUnpackStreamsInFolders is null)
            {
                numUnpackStreamsInFolders = new List<int>(folders.Count);
                for (var i = 0; i < folders.Count; i++)
                {
                    numUnpackStreamsInFolders.Add(1);
                }
            }

            unpackSizes = new List<long>(folders.Count);
            for (var i = 0; i < numUnpackStreamsInFolders.Count; i++)
            {
                // v3.13 incorrectly worked with empty folders
                // v4.07: we check that folder is empty
                var numSubstreams = numUnpackStreamsInFolders[i];
                if (numSubstreams == 0)
                {
                    continue;
                }
#if DEBUG
                Log.Write("#{0} StreamSizes:", i);
#endif
                long sum = 0;
                for (var j = 1; j < numSubstreams; j++)
                {
                    if (type == BlockType.Size)
                    {
                        var size = checked((long)ReadNumber());
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
            {
                type = ReadId();
            }

            var numDigests = 0;
            var numDigestsTotal = 0;
            for (var i = 0; i < folders.Count; i++)
            {
                var numSubstreams = numUnpackStreamsInFolders[i];
                if (numSubstreams != 1 || !folders[i].UnpackCrcDefined)
                {
                    numDigests += numSubstreams;
                }
                numDigestsTotal += numSubstreams;
            }

            digests = null;

            for (; ; )
            {
                if (type == BlockType.Crc)
                {
                    digests = new List<uint?>(numDigestsTotal);

                    var digests2 = ReadHashDigests(numDigests);

                    var digestIndex = 0;
                    for (var i = 0; i < folders.Count; i++)
                    {
                        var numSubstreams = numUnpackStreamsInFolders[i];
                        var folder = folders[i];
                        if (numSubstreams == 1 && folder.UnpackCrcDefined)
                        {
                            digests.Add(folder._unpackCrc.Value);
                        }
                        else
                        {
                            for (var j = 0; j < numSubstreams; j++, digestIndex++)
                            {
                                digests.Add(digests2[digestIndex]);
                            }
                        }
                    }

                    if (digestIndex != numDigests || numDigestsTotal != digests.Count)
                    {
                        Debugger.Break();
                    }
                }
                else if (type == BlockType.End)
                {
                    if (digests is null)
                    {
                        digests = new List<uint?>(numDigestsTotal);
                        for (var i = 0; i < numDigestsTotal; i++)
                        {
                            digests.Add(null);
                        }
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
        out List<uint?> packCrCs,
        out List<CFolder> folders,
        out List<int> numUnpackStreamsInFolders,
        out List<long> unpackSizes,
        out List<uint?> digests
    )
    {
#if DEBUG
        Log.WriteLine("-- ReadStreamsInfo --");
        Log.PushIndent();
#endif
        try
        {
            dataOffset = long.MinValue;
            packSizes = null;
            packCrCs = null;
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
                        ReadPackInfo(out dataOffset, out packSizes, out packCrCs);
                        break;
                    case BlockType.UnpackInfo:
                        ReadUnpackInfo(dataVector, out folders);
                        break;
                    case BlockType.SubStreamsInfo:
                        ReadSubStreamsInfo(
                            folders,
                            out numUnpackStreamsInFolders,
                            out unpackSizes,
                            out digests
                        );
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
            ReadStreamsInfo(
                null,
                out var dataStartPos,
                out var packSizes,
                out var packCrCs,
                out var folders,
                out var numUnpackStreamsInFolders,
                out var unpackSizes,
                out var digests
            );

            dataStartPos += baseOffset;

            var dataVector = new List<byte[]>(folders.Count);
            var packIndex = 0;
            foreach (var folder in folders)
            {
                var oldDataStartPos = dataStartPos;
                var myPackSizes = new long[folder._packStreams.Count];
                for (var i = 0; i < myPackSizes.Length; i++)
                {
                    var packSize = packSizes[packIndex + i];
                    myPackSizes[i] = packSize;
                    dataStartPos += packSize;
                }

                var outStream = DecoderStreamHelper.CreateDecoderStream(
                    _stream,
                    oldDataStartPos,
                    myPackSizes,
                    folder,
                    pass
                );

                var unpackSize = checked((int)folder.GetUnpackSize());
                var data = new byte[unpackSize];
                outStream.ReadExact(data, 0, data.Length);
                if (outStream.ReadByte() >= 0)
                {
                    throw new InvalidOperationException("Decoded stream is longer than expected.");
                }
                dataVector.Add(data);

                if (folder.UnpackCrcDefined)
                {
                    if (
                        Crc.Finish(Crc.Update(Crc.INIT_CRC, data, 0, unpackSize))
                        != folder._unpackCrc
                    )
                    {
                        throw new InvalidOperationException(
                            "Decoded stream does not match expected CRC."
                        );
                    }
                }
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
            var type = ReadId();

            if (type == BlockType.ArchiveProperties)
            {
                ReadArchiveProperties();
                type = ReadId();
            }

            List<byte[]> dataVector = null;
            if (type == BlockType.AdditionalStreamsInfo)
            {
                dataVector = ReadAndDecodePackedStreams(
                    db._startPositionAfterHeader,
                    getTextPassword
                );
                type = ReadId();
            }

            List<long> unpackSizes;
            List<uint?> digests;

            if (type == BlockType.MainStreamsInfo)
            {
                ReadStreamsInfo(
                    dataVector,
                    out db._dataStartPosition,
                    out db._packSizes,
                    out db._packCrCs,
                    out db._folders,
                    out db._numUnpackStreamsVector,
                    out unpackSizes,
                    out digests
                );

                db._dataStartPosition += db._startPositionAfterHeader;
                type = ReadId();
            }
            else
            {
                unpackSizes = new List<long>(db._folders.Count);
                digests = new List<uint?>(db._folders.Count);
                db._numUnpackStreamsVector = new List<int>(db._folders.Count);
                for (var i = 0; i < db._folders.Count; i++)
                {
                    var folder = db._folders[i];
                    unpackSizes.Add(folder.GetUnpackSize());
                    digests.Add(folder._unpackCrc);
                    db._numUnpackStreamsVector.Add(1);
                }
            }

            db._files.Clear();

            if (type == BlockType.End)
            {
                return;
            }

            if (type != BlockType.FilesInfo)
            {
                throw new InvalidOperationException();
            }

            var numFiles = ReadNum();
#if DEBUG
            Log.WriteLine("NumFiles: " + numFiles);
#endif
            db._files = new List<CFileItem>(numFiles);
            for (var i = 0; i < numFiles; i++)
            {
                db._files.Add(new CFileItem());
            }

            var emptyStreamVector = new BitVector(numFiles);
            BitVector emptyFileVector = null;
            BitVector antiFileVector = null;
            var numEmptyStreams = 0;

            for (; ; )
            {
                type = ReadId();
                if (type == BlockType.End)
                {
                    break;
                }

                var size = checked((long)ReadNumber()); // TODO: throw invalid data on negative
                var oldPos = _currentReader.Offset;
                switch (type)
                {
                    case BlockType.Name:
                        using (var streamSwitch = new CStreamSwitch())
                        {
                            streamSwitch.Set(this, dataVector);
#if DEBUG
                            Log.Write("FileNames:");
#endif
                            for (var i = 0; i < db._files.Count; i++)
                            {
                                db._files[i].Name = _currentReader.ReadString();
#if DEBUG
                                Log.Write("  " + db._files[i].Name);
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
                        ReadAttributeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, uint? attr)
                            {
                                // Some third party implementations established an unofficial extension
                                // of the 7z archive format by placing posix file attributes in the high
                                // bits of the windows file attributes. This makes use of the fact that
                                // the official implementation does not perform checks on this value.
                                //
                                // Newer versions of the official 7z GUI client will try to parse this
                                // extension, thus acknowledging the unofficial use of these bits.
                                //
                                // For us it is safe to just discard the upper bits if they are set and
                                // keep the windows attributes from the lower bits (which should be set
                                // properly even if posix file attributes are present, in order to be
                                // compatible with older 7z archive readers)
                                //
                                // Note that the 15th bit is used by some implementations to indicate
                                // presence of the extension, but not all implementations do that so
                                // we can't trust that bit and must ignore it.
                                //
                                if (attr.HasValue && (attr.Value >> 16) != 0)
                                {
                                    attr = attr.Value & 0x7FFFu;
                                }

                                db._files[i].Attrib = attr;
#if DEBUG
                                Log.Write(
                                    "  " + (attr.HasValue ? attr.Value.ToString("x8") : "n/a")
                                );
#endif
                            }
                        );
#if DEBUG
                        Log.WriteLine();
#endif
                        break;
                    case BlockType.EmptyStream:
                        emptyStreamVector = ReadBitVector(numFiles);
#if DEBUG

                        Log.Write("EmptyStream: ");
#endif
                        for (var i = 0; i < emptyStreamVector.Length; i++)
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
                        for (var i = 0; i < numEmptyStreams; i++)
                        {
                            Log.Write(emptyFileVector[i] ? "x" : ".");
                        }
                        Log.WriteLine();
#endif
                        break;
                    case BlockType.Anti:
                        antiFileVector = ReadBitVector(numEmptyStreams);
#if DEBUG
                        Log.Write("Anti: ");
                        for (var i = 0; i < numEmptyStreams; i++)
                        {
                            Log.Write(antiFileVector[i] ? "x" : ".");
                        }
                        Log.WriteLine();
#endif
                        break;
                    case BlockType.StartPos:
#if DEBUG
                        Log.Write("StartPos:");
#endif
                        ReadNumberVector(
                            dataVector,
                            numFiles,
                            delegate(int i, long? startPos)
                            {
                                db._files[i].StartPos = startPos;
#if DEBUG
                                Log.Write(
                                    "  " + (startPos.HasValue ? startPos.Value.ToString() : "n/a")
                                );
#endif
                            }
                        );
#if DEBUG
                        Log.WriteLine();
#endif
                        break;
                    case BlockType.CTime:
#if DEBUG
                        Log.Write("CTime:");
#endif
                        ReadDateTimeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, DateTime? time)
                            {
                                db._files[i].CTime = time;
#if DEBUG
                                Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
#endif
                            }
                        );
#if DEBUG
                        Log.WriteLine();
#endif
                        break;
                    case BlockType.ATime:
#if DEBUG
                        Log.Write("ATime:");
#endif
                        ReadDateTimeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, DateTime? time)
                            {
                                db._files[i].ATime = time;
#if DEBUG
                                Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
#endif
                            }
                        );
#if DEBUG
                        Log.WriteLine();
#endif
                        break;
                    case BlockType.MTime:
#if DEBUG
                        Log.Write("MTime:");
#endif
                        ReadDateTimeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, DateTime? time)
                            {
                                db._files[i].MTime = time;
#if DEBUG
                                Log.Write("  " + (time.HasValue ? time.Value.ToString() : "n/a"));
#endif
                            }
                        );
#if DEBUG
                        Log.WriteLine();
#endif
                        break;
                    case BlockType.Dummy:
#if DEBUG
                        Log.Write("Dummy: " + size);
#endif
                        for (long j = 0; j < size; j++)
                        {
                            if (ReadByte() != 0)
                            {
                                throw new InvalidOperationException();
                            }
                        }
                        break;
                    default:
                        SkipData(size);
                        break;
                }

                // since 0.3 record sizes must be correct
                var checkRecordsSize = (db._majorVersion > 0 || db._minorVersion > 2);
                if (checkRecordsSize && _currentReader.Offset - oldPos != size)
                {
                    throw new InvalidOperationException();
                }
            }

            var emptyFileIndex = 0;
            var sizeIndex = 0;
            for (var i = 0; i < numFiles; i++)
            {
                var file = db._files[i];
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

    public void Open(Stream stream, bool lookForHeader)
    {
        Close();

        _streamOrigin = stream.Position;
        _streamEnding = stream.Length;

        var canScan = lookForHeader ? 0x80000 - 20 : 0;
        while (true)
        {
            // TODO: Check Signature!
            _header = new byte[0x20];
            for (var offset = 0; offset < 0x20; )
            {
                var delta = stream.Read(_header, offset, 0x20 - offset);
                if (delta == 0)
                {
                    throw new EndOfStreamException();
                }

                offset += delta;
            }

            if (
                !lookForHeader
                || _header
                    .AsSpan(0, length: 6)
                    .SequenceEqual<byte>([0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C])
            )
            {
                break;
            }

            if (canScan == 0)
            {
                throw new InvalidFormatException("Unable to find 7z signature");
            }

            canScan--;
            stream.Position = ++_streamOrigin;
        }

        _stream = stream;
    }

    public void Close()
    {
        _stream?.Dispose();

        foreach (var stream in _cachedStreams.Values)
        {
            stream.Dispose();
        }

        _cachedStreams.Clear();
    }

    public ArchiveDatabase ReadDatabase(IPasswordProvider pass)
    {
        var db = new ArchiveDatabase(pass);
        db.Clear();

        db._majorVersion = _header[6];
        db._minorVersion = _header[7];

        if (db._majorVersion != 0)
        {
            throw new InvalidOperationException();
        }

        var crcFromArchive = DataReader.Get32(_header, 8);
        var nextHeaderOffset = (long)DataReader.Get64(_header, 0xC);
        var nextHeaderSize = (long)DataReader.Get64(_header, 0x14);
        var nextHeaderCrc = DataReader.Get32(_header, 0x1C);

        var crc = Crc.INIT_CRC;
        crc = Crc.Update(crc, nextHeaderOffset);
        crc = Crc.Update(crc, nextHeaderSize);
        crc = Crc.Update(crc, nextHeaderCrc);
        crc = Crc.Finish(crc);

        if (crc != crcFromArchive)
        {
            throw new InvalidOperationException();
        }

        db._startPositionAfterHeader = _streamOrigin + 0x20;

        // empty header is ok
        if (nextHeaderSize == 0)
        {
            db.Fill();
            return db;
        }

        if (nextHeaderOffset < 0 || nextHeaderSize < 0 || nextHeaderSize > int.MaxValue)
        {
            throw new InvalidOperationException();
        }

        if (nextHeaderOffset > _streamEnding - db._startPositionAfterHeader)
        {
            throw new InvalidOperationException("nextHeaderOffset is invalid");
        }

        _stream.Seek(nextHeaderOffset, SeekOrigin.Current);

        var header = new byte[nextHeaderSize];
        _stream.ReadExact(header, 0, header.Length);

        if (Crc.Finish(Crc.Update(Crc.INIT_CRC, header, 0, header.Length)) != nextHeaderCrc)
        {
            throw new InvalidOperationException();
        }

        using (var streamSwitch = new CStreamSwitch())
        {
            streamSwitch.Set(this, header);

            var type = ReadId();
            if (type != BlockType.Header)
            {
                if (type != BlockType.EncodedHeader)
                {
                    throw new InvalidOperationException();
                }

                var dataVector = ReadAndDecodePackedStreams(
                    db._startPositionAfterHeader,
                    db.PasswordProvider
                );

                // compressed header without content is odd but ok
                if (dataVector.Count == 0)
                {
                    db.Fill();
                    return db;
                }

                if (dataVector.Count != 1)
                {
                    throw new InvalidOperationException();
                }

                streamSwitch.Set(this, dataVector[0]);

                if (ReadId() != BlockType.Header)
                {
                    throw new InvalidOperationException();
                }
            }

            ReadHeader(db, db.PasswordProvider);
        }
        db.Fill();
        return db;
    }

    internal class CExtractFolderInfo
    {
        internal int _fileIndex;
        internal int _folderIndex;
        internal List<bool> _extractStatuses = new();

        internal CExtractFolderInfo(int fileIndex, int folderIndex)
        {
            _fileIndex = fileIndex;
            _folderIndex = folderIndex;
            if (fileIndex != -1)
            {
                _extractStatuses.Add(true);
            }
        }
    }

    private class FolderUnpackStream : Stream
    {
        private readonly ArchiveDatabase _db;
        private readonly int _startIndex;
        private readonly List<bool> _extractStatuses;

        public FolderUnpackStream(ArchiveDatabase db, int p, int startIndex, List<bool> list)
        {
            _db = db;
            _startIndex = startIndex;
            _extractStatuses = list;
        }

        #region Stream

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush() { }

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        private Stream _stream;
        private long _rem;
        private int _currentIndex;

        private void ProcessEmptyFiles()
        {
            while (
                _currentIndex < _extractStatuses.Count
                && _db._files[_startIndex + _currentIndex].Size == 0
            )
            {
                OpenFile();
                _stream.Dispose();
                _stream = null;
                _currentIndex++;
            }
        }

        private void OpenFile()
        {
            var index = _startIndex + _currentIndex;
#if DEBUG
            Log.WriteLine(_db._files[index].Name);
#endif
            if (_db._files[index].CrcDefined)
            {
                _stream = new CrcCheckStream(_db._files[index].Crc.Value);
            }
            else
            {
                _stream = new MemoryStream();
            }
            _rem = _db._files[index].Size;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            while (count != 0)
            {
                if (_stream != null)
                {
                    var write = count;
                    if (write > _rem)
                    {
                        write = (int)_rem;
                    }
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
                        Debugger.Break();
                        throw new NotSupportedException();
                    }
                    OpenFile();
                }
            }
        }

        #endregion
    }

    private Stream GetCachedDecoderStream(ArchiveDatabase db, int folderIndex)
    {
        if (!_cachedStreams.TryGetValue(folderIndex, out var s))
        {
            var folderInfo = db._folders[folderIndex];
            var packStreamIndex = db._folders[folderIndex]._firstPackStreamId;
            var folderStartPackPos = db.GetFolderStreamPos(folderInfo, 0);
            var count = folderInfo._packStreams.Count;
            var packSizes = new long[count];
            for (var j = 0; j < count; j++)
            {
                packSizes[j] = db._packSizes[packStreamIndex + j];
            }

            s = DecoderStreamHelper.CreateDecoderStream(
                _stream,
                folderStartPackPos,
                packSizes,
                folderInfo,
                db.PasswordProvider
            );
            _cachedStreams.Add(folderIndex, s);
        }
        return s;
    }

    public Stream OpenStream(ArchiveDatabase db, int fileIndex)
    {
        var folderIndex = db._fileIndexToFolderIndexMap[fileIndex];
        var numFilesInFolder = db._numUnpackStreamsVector[folderIndex];
        var firstFileIndex = db._folderStartFileIndex[folderIndex];
        if (firstFileIndex > fileIndex || fileIndex - firstFileIndex >= numFilesInFolder)
        {
            throw new InvalidOperationException();
        }

        var skipCount = fileIndex - firstFileIndex;
        long skipSize = 0;
        for (var i = 0; i < skipCount; i++)
        {
            skipSize += db._files[firstFileIndex + i].Size;
        }

        var s = GetCachedDecoderStream(db, folderIndex);
        s.Position = skipSize;
        return new ReadOnlySubStream(s, db._files[fileIndex].Size);
    }

    public void Extract(ArchiveDatabase db, int[] indices)
    {
        var allFilesMode = (indices is null);

        var numItems = allFilesMode ? db._files.Count : indices.Length;

        if (numItems == 0)
        {
            return;
        }

        var extractFolderInfoVector = new List<CExtractFolderInfo>();
        for (var i = 0; i < numItems; i++)
        {
            var fileIndex = allFilesMode ? i : indices[i];

            var folderIndex = db._fileIndexToFolderIndexMap[fileIndex];
            if (folderIndex == -1)
            {
                extractFolderInfoVector.Add(new CExtractFolderInfo(fileIndex, -1));
                continue;
            }

            if (
                extractFolderInfoVector.Count == 0
                || folderIndex != extractFolderInfoVector.Last()._folderIndex
            )
            {
                extractFolderInfoVector.Add(new CExtractFolderInfo(-1, folderIndex));
            }

            var efi = extractFolderInfoVector.Last();

            var startIndex = db._folderStartFileIndex[folderIndex];
            for (var index = efi._extractStatuses.Count; index <= fileIndex - startIndex; index++)
            {
                efi._extractStatuses.Add(index == fileIndex - startIndex);
            }
        }

        byte[] buffer = null;
        foreach (var efi in extractFolderInfoVector)
        {
            int startIndex;
            if (efi._fileIndex != -1)
            {
                startIndex = efi._fileIndex;
            }
            else
            {
                startIndex = db._folderStartFileIndex[efi._folderIndex];
            }

            var outStream = new FolderUnpackStream(db, 0, startIndex, efi._extractStatuses);

            if (efi._fileIndex != -1)
            {
                continue;
            }

            var folderIndex = efi._folderIndex;
            var folderInfo = db._folders[folderIndex];

            var packStreamIndex = db._folders[folderIndex]._firstPackStreamId;
            var folderStartPackPos = db.GetFolderStreamPos(folderInfo, 0);

            var count = folderInfo._packStreams.Count;
            var packSizes = new long[count];
            for (var j = 0; j < count; j++)
            {
                packSizes[j] = db._packSizes[packStreamIndex + j];
            }

            // TODO: If the decoding fails the last file may be extracted incompletely. Delete it?

            var s = DecoderStreamHelper.CreateDecoderStream(
                _stream,
                folderStartPackPos,
                packSizes,
                folderInfo,
                db.PasswordProvider
            );
            buffer ??= new byte[4 << 10];
            for (; ; )
            {
                var processed = s.Read(buffer, 0, buffer.Length);
                if (processed == 0)
                {
                    break;
                }
                outStream.Write(buffer, 0, processed);
            }
        }
    }

    public IEnumerable<CFileItem> GetFiles(ArchiveDatabase db) => db._files;

    public int GetFileIndex(ArchiveDatabase db, CFileItem item) => db._files.IndexOf(item);

    #endregion
}
