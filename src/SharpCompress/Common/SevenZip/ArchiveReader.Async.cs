using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Deflate64;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.LZMA.Utilities;
using SharpCompress.IO;
using BlockType = SharpCompress.Compressors.LZMA.Utilities.BlockType;

namespace SharpCompress.Common.SevenZip;

internal sealed partial class ArchiveReader
{
    public async ValueTask OpenAsync(
        Stream stream,
        bool lookForHeader,
        CancellationToken cancellationToken = default
    )
    {
        Close();

        _streamOrigin = stream.Position;
        _streamEnding = stream.Length;

        var canScan = lookForHeader ? 0x80000 - 20 : 0;
        while (true)
        {
            // TODO: Check Signature!
            _header = new byte[0x20];
            await stream.ReadExactAsync(_header, 0, 0x20, cancellationToken).ConfigureAwait(false);

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

    public async ValueTask<ArchiveDatabase> ReadDatabaseAsync(
        IPasswordProvider pass,
        CancellationToken cancellationToken = default
    )
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
        await _stream
            .ReadExactAsync(header, 0, header.Length, cancellationToken)
            .ConfigureAwait(false);

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

                var dataVector = await ReadAndDecodePackedStreamsAsync(
                        db._startPositionAfterHeader,
                        db.PasswordProvider,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

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

            await ReadHeaderAsync(db, db.PasswordProvider, cancellationToken).ConfigureAwait(false);
        }
        db.Fill();
        return db;
    }

    private async ValueTask<List<byte[]>> ReadAndDecodePackedStreamsAsync(
        long baseOffset,
        IPasswordProvider pass,
        CancellationToken cancellationToken
    )
    {
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

                var outStream = await DecoderStreamHelper
                    .CreateDecoderStreamAsync(
                        _stream,
                        oldDataStartPos,
                        myPackSizes,
                        folder,
                        pass,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

                var unpackSize = checked((int)folder.GetUnpackSize());
                var data = new byte[unpackSize];
                await outStream
                    .ReadExactAsync(data, 0, data.Length, cancellationToken)
                    .ConfigureAwait(false);
                if (outStream.ReadByte() >= 0)
                {
                    throw new InvalidFormatException("Decoded stream is longer than expected.");
                }
                dataVector.Add(data);

                if (folder.UnpackCrcDefined)
                {
                    if (
                        Crc.Finish(Crc.Update(Crc.INIT_CRC, data, 0, unpackSize))
                        != folder._unpackCrc
                    )
                    {
                        throw new InvalidFormatException(
                            "Decoded stream does not match expected CRC."
                        );
                    }
                }
            }
            return dataVector;
        }
        finally { }
    }

    private async ValueTask ReadHeaderAsync(
        ArchiveDatabase db,
        IPasswordProvider getTextPassword,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var type = ReadId();

            if (type == BlockType.ArchiveProperties)
            {
                ReadArchiveProperties();
                type = ReadId();
            }

            List<byte[]>? dataVector = null;
            if (type == BlockType.AdditionalStreamsInfo)
            {
                dataVector = await ReadAndDecodePackedStreamsAsync(
                        db._startPositionAfterHeader,
                        getTextPassword,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
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
            db._files = new List<CFileItem>(numFiles);
            for (var i = 0; i < numFiles; i++)
            {
                db._files.Add(new CFileItem());
            }

            var emptyStreamVector = new BitVector(numFiles);
            BitVector emptyFileVector = null!;
            BitVector antiFileVector = null!;
            var numEmptyStreams = 0;

            for (; ; )
            {
                type = ReadId();
                if (type == BlockType.End)
                {
                    break;
                }

                var size = checked((long)ReadNumber());
                var oldPos = _currentReader.Offset;
                switch (type)
                {
                    case BlockType.Name:
                        using (var streamSwitch = new CStreamSwitch())
                        {
                            streamSwitch.Set(this, dataVector ?? []);
                            for (var i = 0; i < db._files.Count; i++)
                            {
                                db._files[i].Name = _currentReader.ReadString();
                            }
                        }
                        break;
                    case BlockType.WinAttributes:
                        ReadAttributeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, uint? attr)
                            {
                                db._files[i].ExtendedAttrib = attr;

                                if (attr.HasValue && (attr.Value >> 16) != 0)
                                {
                                    attr = attr.Value & 0x7FFFu;
                                }

                                db._files[i].Attrib = attr;
                            }
                        );
                        break;
                    case BlockType.EmptyStream:
                        emptyStreamVector = ReadBitVector(numFiles);
                        for (var i = 0; i < emptyStreamVector.Length; i++)
                        {
                            if (emptyStreamVector[i])
                            {
                                numEmptyStreams++;
                            }
                            else { }
                        }

                        emptyFileVector = new BitVector(numEmptyStreams);
                        antiFileVector = new BitVector(numEmptyStreams);
                        break;
                    case BlockType.EmptyFile:
                        emptyFileVector = ReadBitVector(numEmptyStreams);
                        break;
                    case BlockType.Anti:
                        antiFileVector = ReadBitVector(numEmptyStreams);
                        break;
                    case BlockType.StartPos:
                        ReadNumberVector(
                            dataVector,
                            numFiles,
                            delegate(int i, long? startPos)
                            {
                                db._files[i].StartPos = startPos;
                            }
                        );
                        break;
                    case BlockType.CTime:
                        ReadDateTimeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, DateTime? time)
                            {
                                db._files[i].CTime = time;
                            }
                        );
                        break;
                    case BlockType.ATime:
                        ReadDateTimeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, DateTime? time)
                            {
                                db._files[i].ATime = time;
                            }
                        );
                        break;
                    case BlockType.MTime:
                        ReadDateTimeVector(
                            dataVector,
                            numFiles,
                            delegate(int i, DateTime? time)
                            {
                                db._files[i].MTime = time;
                            }
                        );
                        break;
                    case BlockType.Dummy:
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
        finally { }
    }
}
