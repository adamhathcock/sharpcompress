using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Rar.UnpackV1.Decode;

namespace SharpCompress.Compressors.Rar.UnpackV1;

internal partial class Unpack
{
    private async Task<bool> UnpReadBufAsync(CancellationToken cancellationToken = default)
    {
        var DataSize = ReadTop - Inp.InAddr; // Data left to process.
        if (DataSize < 0)
        {
            return false;
        }

        BlockHeader.BlockSize -= Inp.InAddr - BlockHeader.BlockStart;
        if (Inp.InAddr > MAX_SIZE / 2)
        {
            if (DataSize > 0)
            {
                Array.Copy(InBuf, inAddr, InBuf, 0, DataSize);
            }

            Inp.InAddr = 0;
            ReadTop = DataSize;
        }
        else
        {
            DataSize = ReadTop;
        }

        var ReadCode = 0;
        if (MAX_SIZE != DataSize)
        {
            ReadCode = await readStream
                .ReadAsync(InBuf, DataSize, MAX_SIZE - DataSize, cancellationToken)
                .ConfigureAwait(false);
        }

        if (ReadCode > 0) // Can be also -1.
        {
            ReadTop += ReadCode;
        }

        ReadBorder = ReadTop - 30;
        BlockHeader.BlockStart = Inp.InAddr;
        if (BlockHeader.BlockSize != -1) // '-1' means not defined yet.
        {
            ReadBorder = Math.Min(ReadBorder, BlockHeader.BlockStart + BlockHeader.BlockSize - 1);
        }
        return ReadCode != -1;
    }

    public async Task Unpack5Async(bool Solid, CancellationToken cancellationToken = default)
    {
        FileExtracted = true;

        if (!Suspended)
        {
            UnpInitData(Solid);
            if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            // Check TablesRead5 to be sure that we read tables at least once
            // regardless of current block header TablePresent flag.
            // So we can safefly use these tables below.
            if (
                !await ReadBlockHeaderAsync(cancellationToken).ConfigureAwait(false)
                || !ReadTables()
                || !TablesRead5
            )
            {
                return;
            }
        }

        while (true)
        {
            UnpPtr &= MaxWinMask;

            if (Inp.InAddr >= ReadBorder)
            {
                var FileDone = false;

                // We use 'while', because for empty block containing only Huffman table,
                // we'll be on the block border once again just after reading the table.
                while (
                    Inp.InAddr > BlockHeader.BlockStart + BlockHeader.BlockSize - 1
                    || Inp.InAddr == BlockHeader.BlockStart + BlockHeader.BlockSize - 1
                        && Inp.InBit >= BlockHeader.BlockBitSize
                )
                {
                    if (BlockHeader.LastBlockInFile)
                    {
                        FileDone = true;
                        break;
                    }
                    if (
                        !await ReadBlockHeaderAsync(cancellationToken).ConfigureAwait(false)
                        || !ReadTables()
                    )
                    {
                        return;
                    }
                }
                if (FileDone || !await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }

            if (
                ((WriteBorder - UnpPtr) & MaxWinMask) < PackDef.MAX_LZ_MATCH + 3
                && WriteBorder != UnpPtr
            )
            {
                UnpWriteBuf();
                if (WrittenFileSize > DestUnpSize)
                {
                    return;
                }

                if (Suspended)
                {
                    FileExtracted = false;
                    return;
                }
            }

            var MainSlot = this.DecodeNumber(LD);
            if (MainSlot < 256)
            {
                Window[UnpPtr++] = (byte)MainSlot;
                continue;
            }
            if (MainSlot >= 262)
            {
                var Length = SlotToLength(MainSlot - 262);

                int DBits;
                uint Distance = 1,
                    DistSlot = this.DecodeNumber(DD);
                if (DistSlot < 4)
                {
                    DBits = 0;
                    Distance += DistSlot;
                }
                else
                {
                    DBits = (int)((DistSlot / 2) - 1);
                    Distance += (2 | (DistSlot & 1)) << DBits;
                }

                if (DBits > 0)
                {
                    if (DBits >= 4)
                    {
                        if (DBits > 4)
                        {
                            Distance += ((Inp.getbits() >> (36 - DBits)) << 4);
                            Inp.AddBits(DBits - 4);
                        }
                        var LowDist = this.DecodeNumber(LDD);
                        Distance += LowDist;
                    }
                    else
                    {
                        Distance += Inp.getbits() >> (32 - DBits);
                        Inp.AddBits(DBits);
                    }
                }

                if (Distance > 0x100)
                {
                    Length++;
                    if (Distance > 0x2000)
                    {
                        Length++;
                        if (Distance > 0x40000)
                        {
                            Length++;
                        }
                    }
                }

                InsertOldDist(Distance);
                LastLength = Length;
                CopyString(Length, Distance);
                continue;
            }
            if (MainSlot == 256)
            {
                var Filter = new UnpackFilter();
                if (
                    !await ReadFilterAsync(Filter, cancellationToken).ConfigureAwait(false)
                    || !AddFilter(Filter)
                )
                {
                    break;
                }

                continue;
            }
            if (MainSlot == 257)
            {
                if (LastLength != 0)
                {
                    CopyString(LastLength, OldDistN(0));
                }

                continue;
            }
            if (MainSlot < 262)
            {
                var DistNum = (int)(MainSlot - 258);
                var Distance = OldDistN(DistNum);
                for (var I = DistNum; I > 0; I--)
                {
                    SetOldDistN(I, OldDistN(I - 1));
                }

                SetOldDistN(0, Distance);

                var LengthSlot = this.DecodeNumber(RD);
                var Length = SlotToLength(LengthSlot);
                LastLength = Length;
                CopyString(Length, Distance);
                continue;
            }
        }
        UnpWriteBuf();
    }

    private async Task<bool> ReadBlockHeaderAsync(CancellationToken cancellationToken = default)
    {
        Header.HeaderSize = 0;

        if (!Inp.ExternalBuffer && Inp.InAddr > ReadTop - 7)
        {
            if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        Inp.faddbits((uint)((8 - Inp.InBit) & 7));

        var BlockFlags = (byte)(Inp.fgetbits() >> 8);
        Inp.faddbits(8);
        var ByteCount = (uint)(((BlockFlags >> 3) & 3) + 1);

        if (ByteCount == 4)
        {
            return false;
        }

        Header.HeaderSize = (int)(2 + ByteCount);

        Header.BlockBitSize = (BlockFlags & 7) + 1;

        var SavedCheckSum = (byte)(Inp.fgetbits() >> 8);
        Inp.faddbits(8);

        var BlockSize = 0;
        for (var I = 0; I < ByteCount; I++)
        {
            BlockSize += (int)(Inp.fgetbits() >> 8) << (I * 8);
            Inp.AddBits(8);
        }

        Header.BlockSize = BlockSize;
        var CheckSum = (byte)(0x5a ^ BlockFlags ^ BlockSize ^ (BlockSize >> 8) ^ (BlockSize >> 16));
        if (CheckSum != SavedCheckSum)
        {
            return false;
        }

        Header.BlockStart = Inp.InAddr;
        ReadBorder = Math.Min(ReadBorder, Header.BlockStart + Header.BlockSize - 1);

        Header.LastBlockInFile = (BlockFlags & 0x40) != 0;
        Header.TablePresent = (BlockFlags & 0x80) != 0;
        return true;
    }

    private async Task<bool> ReadFilterAsync(
        UnpackFilter Filter,
        CancellationToken cancellationToken = default
    )
    {
        if (!Inp.ExternalBuffer && Inp.InAddr > ReadTop - 16)
        {
            if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        Filter.uBlockStart = ReadFilterData();
        Filter.uBlockLength = ReadFilterData();
        if (Filter.BlockLength > MAX_FILTER_BLOCK_SIZE)
        {
            Filter.BlockLength = 0;
        }

        Filter.Type = (byte)(Inp.fgetbits() >> 13);
        Inp.faddbits(3);

        if (Filter.Type == (byte)FilterType.FILTER_DELTA)
        {
            Filter.Channels = (byte)((Inp.fgetbits() >> 11) + 1);
            Inp.faddbits(5);
        }

        return true;
    }
}
