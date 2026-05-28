using System;
using System.IO;
using SharpCompress.Compressors.LZMA.Utilities;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Writes Digests (CRC32 arrays with optional-defined-vector) for 7z headers.
/// </summary>
internal sealed class SevenZipDigestsWriter(uint?[] crcs)
{
    public uint?[] CRCs { get; } = crcs;

    public void Write(Stream stream)
    {
        var defined = new bool[CRCs.Length];
        for (var i = 0; i < CRCs.Length; i++)
        {
            defined[i] = CRCs[i] != null;
        }

        stream.WriteOptionalBoolVector(defined);

        var buf = new byte[4];
        for (var i = 0; i < CRCs.Length; i++)
        {
            if (CRCs[i] is { } crcValue)
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf, crcValue);
                stream.Write(buf, 0, 4);
            }
        }
    }

    public bool HasAnyDefined()
    {
        for (var i = 0; i < CRCs.Length; i++)
        {
            if (CRCs[i] != null)
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Writes PackInfo section: packed stream positions, sizes, and CRCs.
/// </summary>
internal sealed class SevenZipPackInfoWriter
{
    public ulong PackPos { get; init; }
    public ulong[] Sizes { get; init; } = [];
    public uint?[] CRCs { get; init; } = [];

    public void Write(Stream stream)
    {
        stream.WriteEncodedUInt64(PackPos);
        stream.WriteEncodedUInt64((ulong)Sizes.Length);

        // Sizes
        stream.WriteByte((byte)BlockType.Size);
        for (var i = 0; i < Sizes.Length; i++)
        {
            stream.WriteEncodedUInt64(Sizes[i]);
        }

        // CRCs (optional)
        var digests = new SevenZipDigestsWriter(CRCs);
        if (digests.HasAnyDefined())
        {
            stream.WriteByte((byte)BlockType.Crc);
            digests.Write(stream);
        }

        stream.WriteByte((byte)BlockType.End);
    }
}

/// <summary>
/// Writes UnPackInfo section: folder definitions (coders, bind pairs, unpack sizes, CRCs).
/// </summary>
internal sealed class SevenZipUnPackInfoWriter
{
    public CFolder[] Folders { get; init; } = [];

    public void Write(Stream stream)
    {
        stream.WriteByte((byte)BlockType.Folder);

        // Number of folders
        stream.WriteEncodedUInt64((ulong)Folders.Length);

        // External = 0 (inline)
        stream.WriteByte(0);

        // Write each folder's coder definitions
        for (var i = 0; i < Folders.Length; i++)
        {
            WriteFolder(stream, Folders[i]);
        }

        // CodersUnPackSize
        stream.WriteByte((byte)BlockType.CodersUnpackSize);
        for (var i = 0; i < Folders.Length; i++)
        {
            for (var j = 0; j < Folders[i]._unpackSizes.Count; j++)
            {
                stream.WriteEncodedUInt64((ulong)Folders[i]._unpackSizes[j]);
            }
        }

        // UnPackDigests (CRCs per folder)
        var hasCrc = false;
        for (var i = 0; i < Folders.Length; i++)
        {
            if (Folders[i]._unpackCrc != null)
            {
                hasCrc = true;
                break;
            }
        }

        if (hasCrc)
        {
            stream.WriteByte((byte)BlockType.Crc);
            var crcs = new uint?[Folders.Length];
            for (var i = 0; i < Folders.Length; i++)
            {
                crcs[i] = Folders[i]._unpackCrc;
            }
            new SevenZipDigestsWriter(crcs).Write(stream);
        }

        stream.WriteByte((byte)BlockType.End);
    }

    private static void WriteFolder(Stream stream, CFolder folder)
    {
        // NumCoders
        stream.WriteEncodedUInt64((ulong)folder._coders.Count);

        for (var i = 0; i < folder._coders.Count; i++)
        {
            WriteCoder(stream, folder._coders[i]);
        }

        // BindPairs
        for (var i = 0; i < folder._bindPairs.Count; i++)
        {
            stream.WriteEncodedUInt64((ulong)folder._bindPairs[i]._inIndex);
            stream.WriteEncodedUInt64((ulong)folder._bindPairs[i]._outIndex);
        }

        // PackedIndices (only if > 1 packed stream)
        var numPackStreams = folder._packStreams.Count;
        if (numPackStreams > 1)
        {
            for (var i = 0; i < numPackStreams; i++)
            {
                stream.WriteEncodedUInt64((ulong)folder._packStreams[i]);
            }
        }
    }

    private static void WriteCoder(Stream stream, CCoderInfo coder)
    {
        var codecIdLength = coder._methodId.GetLength();
        byte attributes = (byte)(codecIdLength & 0x0F);

        var isComplex = coder._numInStreams != 1 || coder._numOutStreams != 1;
        if (isComplex)
        {
            attributes |= 0x10;
        }

        var hasProperties = coder._props != null && coder._props.Length > 0;
        if (hasProperties)
        {
            attributes |= 0x20;
        }

        stream.WriteByte(attributes);

        // Codec ID bytes (big-endian, most significant byte first)
        var codecId = new byte[codecIdLength];
        var id = coder._methodId._id;
        for (var i = codecIdLength - 1; i >= 0; i--)
        {
            codecId[i] = (byte)(id & 0xFF);
            id >>= 8;
        }
        stream.Write(codecId, 0, codecIdLength);

        if (isComplex)
        {
            stream.WriteEncodedUInt64((ulong)coder._numInStreams);
            stream.WriteEncodedUInt64((ulong)coder._numOutStreams);
        }

        if (hasProperties)
        {
            stream.WriteEncodedUInt64((ulong)coder._props!.Length);
            stream.Write(coder._props);
        }
    }
}

/// <summary>
/// Writes SubStreamsInfo section: per-file unpack sizes and CRCs within folders.
/// </summary>
internal sealed class SevenZipSubStreamsInfoWriter
{
    public CFolder[] Folders { get; init; } = [];
    public ulong[] NumUnPackStreamsInFolders { get; init; } = [];
    public ulong[] UnPackSizes { get; init; } = [];
    public uint?[] CRCs { get; init; } = [];

    public void Write(Stream stream)
    {
        var numFolders = (ulong)Folders.Length;

        // NumUnPackStream per folder (skip if all folders have exactly 1 stream)
        var totalStreams = 0UL;
        var allSingle = true;
        for (var i = 0; i < NumUnPackStreamsInFolders.Length; i++)
        {
            totalStreams += NumUnPackStreamsInFolders[i];
            if (NumUnPackStreamsInFolders[i] != 1)
            {
                allSingle = false;
            }
        }

        if (!allSingle)
        {
            stream.WriteByte((byte)BlockType.NumUnpackStream);
            for (var i = 0; i < NumUnPackStreamsInFolders.Length; i++)
            {
                stream.WriteEncodedUInt64(NumUnPackStreamsInFolders[i]);
            }
        }

        // UnPackSizes - write all except the last per folder (it's implicit from folder unpack size).
        // Only emit the Size block when at least one folder has multiple substreams.
        if (UnPackSizes.Length > 0 && !allSingle)
        {
            stream.WriteByte((byte)BlockType.Size);

            var sizeIndex = 0;
            for (var i = 0; i < NumUnPackStreamsInFolders.Length; i++)
            {
                var numStreams = NumUnPackStreamsInFolders[i];
                for (var j = 1UL; j < numStreams; j++)
                {
                    stream.WriteEncodedUInt64(UnPackSizes[sizeIndex++]);
                }
                sizeIndex++; // skip the last (implicit)
            }
        }

        // Digests for streams with unknown CRCs
        var digests = new SevenZipDigestsWriter(CRCs);
        if (digests.HasAnyDefined())
        {
            stream.WriteByte((byte)BlockType.Crc);
            digests.Write(stream);
        }

        stream.WriteByte((byte)BlockType.End);
    }
}

/// <summary>
/// Writes the complete StreamsInfo section (PackInfo + UnPackInfo + SubStreamsInfo).
/// </summary>
internal sealed class SevenZipStreamsInfoWriter
{
    public SevenZipPackInfoWriter? PackInfo { get; init; }
    public SevenZipUnPackInfoWriter? UnPackInfo { get; init; }
    public SevenZipSubStreamsInfoWriter? SubStreamsInfo { get; init; }

    public void Write(Stream stream)
    {
        if (PackInfo != null)
        {
            stream.WriteByte((byte)BlockType.PackInfo);
            PackInfo.Write(stream);
        }

        if (UnPackInfo != null)
        {
            stream.WriteByte((byte)BlockType.UnpackInfo);
            UnPackInfo.Write(stream);
        }

        if (SubStreamsInfo != null)
        {
            stream.WriteByte((byte)BlockType.SubStreamsInfo);
            SubStreamsInfo.Write(stream);
        }

        stream.WriteByte((byte)BlockType.End);
    }
}
