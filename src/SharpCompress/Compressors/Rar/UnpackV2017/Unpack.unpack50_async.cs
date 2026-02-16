#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using static SharpCompress.Compressors.Rar.UnpackV2017.PackDef;
using static SharpCompress.Compressors.Rar.UnpackV2017.UnpackGlobal;
using size_t = System.UInt32;

namespace SharpCompress.Compressors.Rar.UnpackV2017;

internal partial class Unpack
{
    private async Task Unpack5Async(bool Solid, CancellationToken cancellationToken = default)
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
                !await ReadBlockHeaderAsync(Inp, cancellationToken).ConfigureAwait(false)
                || !await ReadTablesAsync(Inp, cancellationToken).ConfigureAwait(false)
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
                        !await ReadBlockHeaderAsync(Inp, cancellationToken).ConfigureAwait(false)
                        || !await ReadTablesAsync(Inp, cancellationToken).ConfigureAwait(false)
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

            if (((WriteBorder - UnpPtr) & MaxWinMask) < MAX_LZ_MATCH + 3 && WriteBorder != UnpPtr)
            {
                await UnpWriteBufAsync(cancellationToken).ConfigureAwait(false);
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

            var MainSlot = DecodeNumber(Inp, BlockTables.LD);
            if (MainSlot < 256)
            {
                if (Fragmented)
                {
                    FragWindow[UnpPtr++] = (byte)MainSlot;
                }
                else
                {
                    Window[UnpPtr++] = (byte)MainSlot;
                }
                continue;
            }
            if (MainSlot >= 262)
            {
                var Length = SlotToLength(Inp, MainSlot - 262);

                uint DBits,
                    Distance = 1,
                    DistSlot = DecodeNumber(Inp, BlockTables.DD);
                if (DistSlot < 4)
                {
                    DBits = 0;
                    Distance += DistSlot;
                }
                else
                {
                    DBits = (DistSlot / 2) - 1;
                    Distance += (2 | (DistSlot & 1)) << (int)DBits;
                }

                if (DBits > 0)
                {
                    if (DBits >= 4)
                    {
                        if (DBits > 4)
                        {
                            Distance += ((Inp.getbits32() >> (int)(36 - DBits)) << 4);
                            Inp.addbits(DBits - 4);
                        }

                        var LowDist = DecodeNumber(Inp, BlockTables.LDD);
                        Distance += LowDist;
                    }
                    else
                    {
                        Distance += Inp.getbits32() >> (int)(32 - DBits);
                        Inp.addbits(DBits);
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
                if (Fragmented)
                {
                    FragWindow.CopyString(Length, Distance, ref UnpPtr, MaxWinMask);
                }
                else
                {
                    CopyString(Length, Distance);
                }
                continue;
            }
            if (MainSlot == 256)
            {
                var Filter = new UnpackFilter();
                if (
                    !await ReadFilterAsync(Inp, Filter, cancellationToken).ConfigureAwait(false)
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
                    if (Fragmented)
                    {
                        FragWindow.CopyString(LastLength, OldDist[0], ref UnpPtr, MaxWinMask);
                    }
                    else
                    {
                        CopyString(LastLength, OldDist[0]);
                    }
                }
                continue;
            }
            if (MainSlot < 262)
            {
                var DistNum = MainSlot - 258;
                var Distance = OldDist[DistNum];
                for (var I = DistNum; I > 0; I--)
                {
                    OldDist[I] = OldDist[I - 1];
                }

                OldDist[0] = Distance;

                var LengthSlot = DecodeNumber(Inp, BlockTables.RD);
                var Length = SlotToLength(Inp, LengthSlot);
                LastLength = Length;
                if (Fragmented)
                {
                    FragWindow.CopyString(Length, Distance, ref UnpPtr, MaxWinMask);
                }
                else
                {
                    CopyString(Length, Distance);
                }

                continue;
            }
        }
        await UnpWriteBufAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ReadFilterAsync(
        BitInput Inp,
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

        Filter.BlockStart = ReadFilterData(Inp);
        Filter.BlockLength = ReadFilterData(Inp);
        if (Filter.BlockLength > MAX_FILTER_BLOCK_SIZE)
        {
            Filter.BlockLength = 0;
        }

        Filter.Type = (byte)(Inp.fgetbits() >> 13);
        Inp.faddbits(3);

        if (Filter.Type == FILTER_DELTA)
        {
            Filter.Channels = (byte)((Inp.fgetbits() >> 11) + 1);
            Inp.faddbits(5);
        }

        return true;
    }

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
                Buffer.BlockCopy(Inp.InBuf, Inp.InAddr, Inp.InBuf, 0, DataSize);
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
            ReadCode = await UnpIO_UnpReadAsync(
                    Inp.InBuf,
                    DataSize,
                    MAX_SIZE - DataSize,
                    cancellationToken
                )
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

    private async Task UnpWriteBufAsync(CancellationToken cancellationToken = default)
    {
        var WrittenBorder = WrPtr;
        var FullWriteSize = (UnpPtr - WrittenBorder) & MaxWinMask;
        var WriteSizeLeft = FullWriteSize;
        var NotAllFiltersProcessed = false;

        for (var I = 0; I < Filters.Count; I++)
        {
            var flt = Filters[I];
            if (flt.Type == FILTER_NONE)
            {
                continue;
            }

            if (flt.NextWindow)
            {
                if (((flt.BlockStart - WrPtr) & MaxWinMask) <= FullWriteSize)
                {
                    flt.NextWindow = false;
                }
                continue;
            }

            var BlockStart = flt.BlockStart;
            var BlockLength = flt.BlockLength;
            if (((BlockStart - WrittenBorder) & MaxWinMask) < WriteSizeLeft)
            {
                if (WrittenBorder != BlockStart)
                {
                    await UnpWriteAreaAsync(WrittenBorder, BlockStart, cancellationToken)
                        .ConfigureAwait(false);
                    WrittenBorder = BlockStart;
                    WriteSizeLeft = (UnpPtr - WrittenBorder) & MaxWinMask;
                }
                if (BlockLength <= WriteSizeLeft)
                {
                    if (BlockLength > 0)
                    {
                        var BlockEnd = (BlockStart + BlockLength) & MaxWinMask;

                        FilterSrcMemory = EnsureCapacity(
                            FilterSrcMemory,
                            checked((int)BlockLength)
                        );
                        var Mem = FilterSrcMemory;
                        if (BlockStart < BlockEnd || BlockEnd == 0)
                        {
                            if (Fragmented)
                            {
                                FragWindow.CopyData(Mem, 0, BlockStart, BlockLength);
                            }
                            else
                            {
                                Buffer.BlockCopy(Window, (int)BlockStart, Mem, 0, (int)BlockLength);
                            }
                        }
                        else
                        {
                            var FirstPartLength = MaxWinSize - BlockStart;
                            if (Fragmented)
                            {
                                FragWindow.CopyData(Mem, 0, BlockStart, FirstPartLength);
                                FragWindow.CopyData(Mem, FirstPartLength, 0, BlockEnd);
                            }
                            else
                            {
                                Buffer.BlockCopy(
                                    Window,
                                    (int)BlockStart,
                                    Mem,
                                    0,
                                    (int)FirstPartLength
                                );
                                Buffer.BlockCopy(
                                    Window,
                                    0,
                                    Mem,
                                    (int)FirstPartLength,
                                    (int)BlockEnd
                                );
                            }
                        }

                        var OutMem = ApplyFilter(Mem, BlockLength, flt);

                        Filters[I].Type = FILTER_NONE;

                        if (OutMem != null)
                        {
                            await UnpIO_UnpWriteAsync(OutMem, 0, BlockLength, cancellationToken)
                                .ConfigureAwait(false);
                            WrittenFileSize += BlockLength;
                        }

                        WrittenBorder = BlockEnd;
                        WriteSizeLeft = (UnpPtr - WrittenBorder) & MaxWinMask;
                    }
                }
                else
                {
                    NotAllFiltersProcessed = true;
                    for (var J = I; J < Filters.Count; J++)
                    {
                        var fltj = Filters[J];
                        if (
                            fltj.Type != FILTER_NONE
                            && fltj.NextWindow == false
                            && ((fltj.BlockStart - WrPtr) & MaxWinMask) < FullWriteSize
                        )
                        {
                            fltj.NextWindow = true;
                        }
                    }
                    break;
                }
            }
        }

        var EmptyCount = 0;
        for (var I = 0; I < Filters.Count; I++)
        {
            if (EmptyCount > 0)
            {
                Filters[I - EmptyCount] = Filters[I];
            }

            if (Filters[I].Type == FILTER_NONE)
            {
                EmptyCount++;
            }
        }
        if (EmptyCount > 0)
        {
            Filters.RemoveRange(Filters.Count - EmptyCount, EmptyCount);
        }

        if (!NotAllFiltersProcessed)
        {
            await UnpWriteAreaAsync(WrittenBorder, UnpPtr, cancellationToken).ConfigureAwait(false);
            WrPtr = UnpPtr;
        }

        WriteBorder = (UnpPtr + Math.Min(MaxWinSize, UNPACK_MAX_WRITE)) & MaxWinMask;

        if (
            WriteBorder == UnpPtr
            || WrPtr != UnpPtr
                && ((WrPtr - UnpPtr) & MaxWinMask) < ((WriteBorder - UnpPtr) & MaxWinMask)
        )
        {
            WriteBorder = WrPtr;
        }
    }

    private async Task UnpWriteAreaAsync(
        size_t StartPtr,
        size_t EndPtr,
        CancellationToken cancellationToken = default
    )
    {
        if (EndPtr != StartPtr)
        {
            UnpSomeRead = true;
        }

        if (EndPtr < StartPtr)
        {
            UnpAllBuf = true;
        }

        if (Fragmented)
        {
            var SizeToWrite = (EndPtr - StartPtr) & MaxWinMask;
            while (SizeToWrite > 0)
            {
                var BlockSize = FragWindow.GetBlockSize(StartPtr, SizeToWrite);
                FragWindow.GetBuffer(StartPtr, out var __buffer, out var __offset);
                await UnpWriteDataAsync(__buffer, __offset, BlockSize, cancellationToken)
                    .ConfigureAwait(false);
                SizeToWrite -= BlockSize;
                StartPtr = (StartPtr + BlockSize) & MaxWinMask;
            }
        }
        else if (EndPtr < StartPtr)
        {
            await UnpWriteDataAsync(Window, StartPtr, MaxWinSize - StartPtr, cancellationToken)
                .ConfigureAwait(false);
            await UnpWriteDataAsync(Window, 0, EndPtr, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await UnpWriteDataAsync(Window, StartPtr, EndPtr - StartPtr, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task UnpWriteDataAsync(
        byte[] Data,
        size_t offset,
        size_t Size,
        CancellationToken cancellationToken = default
    )
    {
        if (WrittenFileSize >= DestUnpSize)
        {
            return;
        }

        var WriteSize = Size;
        var LeftToWrite = DestUnpSize - WrittenFileSize;
        if (WriteSize > LeftToWrite)
        {
            WriteSize = (size_t)LeftToWrite;
        }

        await UnpIO_UnpWriteAsync(Data, offset, WriteSize, cancellationToken).ConfigureAwait(false);
        WrittenFileSize += Size;
    }

    private async Task<bool> ReadBlockHeaderAsync(
        BitInput Inp,
        CancellationToken cancellationToken = default
    )
    {
        BlockHeader.HeaderSize = 0;

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
        var ByteCount = (uint)(((BlockFlags >> 3) & 3) + 1); // Block size byte count.

        if (ByteCount == 4)
        {
            return false;
        }

        BlockHeader.HeaderSize = (int)(2 + ByteCount);

        BlockHeader.BlockBitSize = (BlockFlags & 7) + 1;

        var SavedCheckSum = (byte)(Inp.fgetbits() >> 8);
        Inp.faddbits(8);

        var BlockSize = 0;
        for (uint I = 0; I < ByteCount; I++)
        {
            BlockSize += (int)((Inp.fgetbits() >> 8) << (int)(I * 8));
            Inp.addbits(8);
        }

        BlockHeader.BlockSize = BlockSize;
        var CheckSum = (byte)(0x5a ^ BlockFlags ^ BlockSize ^ (BlockSize >> 8) ^ (BlockSize >> 16));
        if (CheckSum != SavedCheckSum)
        {
            return false;
        }

        BlockHeader.BlockStart = Inp.InAddr;
        ReadBorder = Math.Min(ReadBorder, BlockHeader.BlockStart + BlockHeader.BlockSize - 1);

        BlockHeader.LastBlockInFile = (BlockFlags & 0x40) != 0;
        BlockHeader.TablePresent = (BlockFlags & 0x80) != 0;
        return true;
    }

    private async Task<bool> ReadTablesAsync(
        BitInput Inp,
        CancellationToken cancellationToken = default
    )
    {
        if (!BlockHeader.TablePresent)
        {
            return true;
        }

        if (!Inp.ExternalBuffer && Inp.InAddr > ReadTop - 25)
        {
            if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        var BitLength = new byte[checked((int)BC)];
        for (int I = 0; I < BC; I++)
        {
            uint Length = (byte)(Inp.fgetbits() >> 12);
            Inp.faddbits(4);
            if (Length == 15)
            {
                uint ZeroCount = (byte)(Inp.fgetbits() >> 12);
                Inp.faddbits(4);
                if (ZeroCount == 0)
                {
                    BitLength[I] = 15;
                }
                else
                {
                    ZeroCount += 2;
                    while (ZeroCount-- > 0 && I < BitLength.Length)
                    {
                        BitLength[I++] = 0;
                    }

                    I--;
                }
            }
            else
            {
                BitLength[I] = (byte)Length;
            }
        }

        MakeDecodeTables(BitLength, 0, BlockTables.BD, BC);

        var Table = new byte[checked((int)HUFF_TABLE_SIZE)];
        const int TableSize = checked((int)HUFF_TABLE_SIZE);
        for (int I = 0; I < TableSize; )
        {
            if (!Inp.ExternalBuffer && Inp.InAddr > ReadTop - 5)
            {
                if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }

            var Number = DecodeNumber(Inp, BlockTables.BD);
            if (Number < 16)
            {
                Table[I] = (byte)Number;
                I++;
            }
            else if (Number < 18)
            {
                uint N;
                if (Number == 16)
                {
                    N = (Inp.fgetbits() >> 13) + 3;
                    Inp.faddbits(3);
                }
                else
                {
                    N = (Inp.fgetbits() >> 9) + 11;
                    Inp.faddbits(7);
                }
                if (I == 0)
                {
                    // We cannot have "repeat previous" code at the first position.
                    // Multiple such codes would shift Inp position without changing I,
                    // which can lead to reading beyond of Inp boundary in mutithreading
                    // mode, where Inp.ExternalBuffer disables bounds check and we just
                    // reserve a lot of buffer space to not need such check normally.
                    return false;
                }
                else
                {
                    while (N-- > 0 && I < TableSize)
                    {
                        Table[I] = Table[I - 1];
                        I++;
                    }
                }
            }
            else
            {
                uint N;
                if (Number == 18)
                {
                    N = (Inp.fgetbits() >> 13) + 3;
                    Inp.faddbits(3);
                }
                else
                {
                    N = (Inp.fgetbits() >> 9) + 11;
                    Inp.faddbits(7);
                }
                while (N-- > 0 && I < TableSize)
                {
                    Table[I++] = 0;
                }
            }
        }
        TablesRead5 = true;
        if (!Inp.ExternalBuffer && Inp.InAddr > ReadTop)
        {
            return false;
        }

        MakeDecodeTables(Table, 0, BlockTables.LD, NC);
        MakeDecodeTables(Table, (int)NC, BlockTables.DD, DC);
        MakeDecodeTables(Table, (int)(NC + DC), BlockTables.LDD, LDC);
        MakeDecodeTables(Table, (int)(NC + DC + LDC), BlockTables.RD, RC);
        return true;
    }
}
