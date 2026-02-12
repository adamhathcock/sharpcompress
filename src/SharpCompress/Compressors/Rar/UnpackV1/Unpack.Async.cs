using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar.UnpackV1.Decode;
using SharpCompress.Compressors.Rar.UnpackV1.PPM;
using SharpCompress.Compressors.Rar.VM;

namespace SharpCompress.Compressors.Rar.UnpackV1;

internal sealed partial class Unpack
{
    public async Task DoUnpackAsync(
        FileHeader fileHeader,
        Stream readStream,
        Stream writeStream,
        CancellationToken cancellationToken = default
    )
    {
        destUnpSize = fileHeader.UncompressedSize;
        this.fileHeader = fileHeader;
        this.readStream = readStream;
        this.writeStream = writeStream;
        if (!fileHeader.IsSolid)
        {
            Init();
        }
        suspended = false;
        await DoUnpackAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DoUnpackAsync(CancellationToken cancellationToken = default)
    {
        if (fileHeader.CompressionMethod == 0)
        {
            await UnstoreFileAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        switch (fileHeader.CompressionAlgorithm)
        {
            case 15:
                await unpack15Async(fileHeader.IsSolid, cancellationToken).ConfigureAwait(false);
                break;
            case 20:
            case 26:
                await unpack20Async(fileHeader.IsSolid, cancellationToken).ConfigureAwait(false);
                break;
            case 29:
            case 36:
                await Unpack29Async(fileHeader.IsSolid, cancellationToken).ConfigureAwait(false);
                break;
            case 50:
                await Unpack5Async(fileHeader.IsSolid, cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new InvalidFormatException(
                    "unknown rar compression version " + fileHeader.CompressionAlgorithm
                );
        }
    }

    private async Task UnstoreFileAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[(int)Math.Min(0x10000, destUnpSize)];
        do
        {
            var code = await readStream
                .ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                .ConfigureAwait(false);
            if (code == 0 || code == -1)
            {
                break;
            }
            code = code < destUnpSize ? code : (int)destUnpSize;
            await writeStream.WriteAsync(buffer, 0, code, cancellationToken).ConfigureAwait(false);
            destUnpSize -= code;
        } while (!suspended && destUnpSize > 0);
    }

    private async Task Unpack29Async(bool solid, CancellationToken cancellationToken = default)
    {
        int[] DDecode = new int[PackDef.DC];
        byte[] DBits = new byte[PackDef.DC];

        int Bits;

        if (DDecode[1] == 0)
        {
            int Dist = 0,
                BitLength = 0,
                Slot = 0;
            for (var I = 0; I < DBitLengthCounts.Length; I++, BitLength++)
            {
                var count = DBitLengthCounts[I];
                for (var J = 0; J < count; J++, Slot++, Dist += (1 << BitLength))
                {
                    DDecode[Slot] = Dist;
                    DBits[Slot] = (byte)BitLength;
                }
            }
        }

        FileExtracted = true;

        if (!suspended)
        {
            UnpInitData(solid);
            if (!await unpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            if ((!solid || !tablesRead) && !ReadTables())
            {
                return;
            }
        }

        if (ppmError)
        {
            return;
        }

        while (true)
        {
            unpPtr &= PackDef.MAXWINMASK;

            if (inAddr > readBorder)
            {
                if (!await unpReadBufAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }

            if (((wrPtr - unpPtr) & PackDef.MAXWINMASK) < 260 && wrPtr != unpPtr)
            {
                UnpWriteBuf();
                if (destUnpSize < 0)
                {
                    return;
                }
                if (suspended)
                {
                    FileExtracted = false;
                    return;
                }
            }
            if (unpBlockType == BlockTypes.BLOCK_PPM)
            {
                var Ch = ppm.DecodeChar();
                if (Ch == -1)
                {
                    ppmError = true;
                    break;
                }
                if (Ch == PpmEscChar)
                {
                    var NextCh = ppm.DecodeChar();
                    if (NextCh == 0)
                    {
                        if (!ReadTables())
                        {
                            break;
                        }
                        continue;
                    }
                    if (NextCh == 2 || NextCh == -1)
                    {
                        break;
                    }
                    if (NextCh == 3)
                    {
                        if (!ReadVMCodePPM())
                        {
                            break;
                        }
                        continue;
                    }
                    if (NextCh == 4)
                    {
                        int Distance = 0,
                            Length = 0;
                        var failed = false;
                        for (var I = 0; I < 4 && !failed; I++)
                        {
                            var ch = ppm.DecodeChar();
                            if (ch == -1)
                            {
                                failed = true;
                            }
                            else
                            {
                                if (I == 3)
                                {
                                    Length = ch & 0xff;
                                }
                                else
                                {
                                    Distance = (Distance << 8) + (ch & 0xff);
                                }
                            }
                        }
                        if (failed)
                        {
                            break;
                        }
                        CopyString(Length + 32, Distance + 2);
                        continue;
                    }
                    if (NextCh == 5)
                    {
                        var Length = ppm.DecodeChar();
                        if (Length == -1)
                        {
                            break;
                        }
                        CopyString(Length + 4, 1);
                        continue;
                    }
                }
                window[unpPtr++] = (byte)Ch;
                continue;
            }

            var Number = this.decodeNumber(LD);
            if (Number < 256)
            {
                window[unpPtr++] = (byte)Number;
                continue;
            }
            if (Number >= 271)
            {
                var Length = LDecode[Number -= 271] + 3;
                if ((Bits = LBits[Number]) > 0)
                {
                    Length += Utility.URShift(GetBits(), (16 - Bits));
                    AddBits(Bits);
                }

                var DistNumber = this.decodeNumber(DD);
                var Distance = DDecode[DistNumber] + 1;
                if ((Bits = DBits[DistNumber]) > 0)
                {
                    if (DistNumber > 9)
                    {
                        if (Bits > 4)
                        {
                            Distance += ((Utility.URShift(GetBits(), (20 - Bits))) << 4);
                            AddBits(Bits - 4);
                        }
                        if (lowDistRepCount > 0)
                        {
                            lowDistRepCount--;
                            Distance += prevLowDist;
                        }
                        else
                        {
                            var LowDist = this.decodeNumber(LDD);
                            if (LowDist == 16)
                            {
                                lowDistRepCount = PackDef.LOW_DIST_REP_COUNT - 1;
                                Distance += prevLowDist;
                            }
                            else
                            {
                                Distance += LowDist;
                                prevLowDist = LowDist;
                            }
                        }
                    }
                    else
                    {
                        Distance += Utility.URShift(GetBits(), (16 - Bits));
                        AddBits(Bits);
                    }
                }

                if (Distance >= 0x2000)
                {
                    Length++;
                    if (Distance >= 0x40000L)
                    {
                        Length++;
                    }
                }

                InsertOldDist(Distance);
                InsertLastMatch(Length, Distance);
                CopyString(Length, Distance);
                continue;
            }
            if (Number == 256)
            {
                if (!ReadEndOfBlock())
                {
                    break;
                }
                continue;
            }
            if (Number == 257)
            {
                if (!ReadVMCode())
                {
                    break;
                }
                continue;
            }
            if (Number == 258)
            {
                if (lastLength != 0)
                {
                    CopyString(lastLength, lastDist);
                }
                continue;
            }
            if (Number < 263)
            {
                var DistNum = Number - 259;
                var Distance = oldDist[DistNum];
                for (var I = DistNum; I > 0; I--)
                {
                    oldDist[I] = oldDist[I - 1];
                }
                oldDist[0] = Distance;

                var LengthNumber = this.decodeNumber(RD);
                var Length = LDecode[LengthNumber] + 2;
                if ((Bits = LBits[LengthNumber]) > 0)
                {
                    Length += Utility.URShift(GetBits(), (16 - Bits));
                    AddBits(Bits);
                }
                InsertLastMatch(Length, Distance);
                CopyString(Length, Distance);
                continue;
            }
            if (Number < 272)
            {
                var Distance = SDDecode[Number -= 263] + 1;
                if ((Bits = SDBits[Number]) > 0)
                {
                    Distance += Utility.URShift(GetBits(), (16 - Bits));
                    AddBits(Bits);
                }
                InsertOldDist(Distance);
                InsertLastMatch(2, Distance);
                CopyString(2, Distance);
            }
        }
        UnpWriteBuf();
    }

    private async Task UnpWriteBufAsync(CancellationToken cancellationToken = default)
    {
        var WrittenBorder = wrPtr;
        var WriteSize = (unpPtr - WrittenBorder) & PackDef.MAXWINMASK;
        for (var I = 0; I < prgStack.Count; I++)
        {
            var flt = prgStack[I];
            if (flt is null)
            {
                continue;
            }
            if (flt.NextWindow)
            {
                flt.NextWindow = false;
                continue;
            }
            var BlockStart = flt.BlockStart;
            var BlockLength = flt.BlockLength;
            if (((BlockStart - WrittenBorder) & PackDef.MAXWINMASK) < WriteSize)
            {
                if (WrittenBorder != BlockStart)
                {
                    await UnpWriteAreaAsync(WrittenBorder, BlockStart, cancellationToken)
                        .ConfigureAwait(false);
                    WrittenBorder = BlockStart;
                    WriteSize = (unpPtr - WrittenBorder) & PackDef.MAXWINMASK;
                }
                if (BlockLength <= WriteSize)
                {
                    var BlockEnd = (BlockStart + BlockLength) & PackDef.MAXWINMASK;
                    if (BlockStart < BlockEnd || BlockEnd == 0)
                    {
                        rarVM.setMemory(0, window, BlockStart, BlockLength);
                    }
                    else
                    {
                        var FirstPartLength = PackDef.MAXWINSIZE - BlockStart;
                        rarVM.setMemory(0, window, BlockStart, FirstPartLength);
                        rarVM.setMemory(FirstPartLength, window, 0, BlockEnd);
                    }

                    var ParentPrg = filters[flt.ParentFilter].Program;
                    var Prg = flt.Program;

                    if (ParentPrg.GlobalData.Count > RarVM.VM_FIXEDGLOBALSIZE)
                    {
                        Prg.GlobalData.Clear();
                        for (
                            var i = 0;
                            i < ParentPrg.GlobalData.Count - RarVM.VM_FIXEDGLOBALSIZE;
                            i++
                        )
                        {
                            Prg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i] = ParentPrg.GlobalData[
                                RarVM.VM_FIXEDGLOBALSIZE + i
                            ];
                        }
                    }

                    ExecuteCode(Prg);

                    if (Prg.GlobalData.Count > RarVM.VM_FIXEDGLOBALSIZE)
                    {
                        if (ParentPrg.GlobalData.Count < Prg.GlobalData.Count)
                        {
                            ParentPrg.GlobalData.SetSize(Prg.GlobalData.Count);
                        }

                        for (var i = 0; i < Prg.GlobalData.Count - RarVM.VM_FIXEDGLOBALSIZE; i++)
                        {
                            ParentPrg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i] = Prg.GlobalData[
                                RarVM.VM_FIXEDGLOBALSIZE + i
                            ];
                        }
                    }
                    else
                    {
                        ParentPrg.GlobalData.Clear();
                    }

                    var FilteredDataOffset = Prg.FilteredDataOffset;
                    var FilteredDataSize = Prg.FilteredDataSize;
                    var FilteredData = ArrayPool<byte>.Shared.Rent(FilteredDataSize);
                    try
                    {
                        Array.Copy(
                            rarVM.Mem,
                            FilteredDataOffset,
                            FilteredData,
                            0,
                            FilteredDataSize
                        );

                        prgStack[I] = null;
                        while (I + 1 < prgStack.Count)
                        {
                            var NextFilter = prgStack[I + 1];
                            if (
                                NextFilter is null
                                || NextFilter.BlockStart != BlockStart
                                || NextFilter.BlockLength != FilteredDataSize
                                || NextFilter.NextWindow
                            )
                            {
                                break;
                            }

                            rarVM.setMemory(0, FilteredData, 0, FilteredDataSize);

                            var pPrg = filters[NextFilter.ParentFilter].Program;
                            var NextPrg = NextFilter.Program;

                            if (pPrg.GlobalData.Count > RarVM.VM_FIXEDGLOBALSIZE)
                            {
                                NextPrg.GlobalData.SetSize(pPrg.GlobalData.Count);

                                for (
                                    var i = 0;
                                    i < pPrg.GlobalData.Count - RarVM.VM_FIXEDGLOBALSIZE;
                                    i++
                                )
                                {
                                    NextPrg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i] =
                                        pPrg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i];
                                }
                            }

                            ExecuteCode(NextPrg);

                            if (NextPrg.GlobalData.Count > RarVM.VM_FIXEDGLOBALSIZE)
                            {
                                if (pPrg.GlobalData.Count < NextPrg.GlobalData.Count)
                                {
                                    pPrg.GlobalData.SetSize(NextPrg.GlobalData.Count);
                                }

                                for (
                                    var i = 0;
                                    i < NextPrg.GlobalData.Count - RarVM.VM_FIXEDGLOBALSIZE;
                                    i++
                                )
                                {
                                    pPrg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i] =
                                        NextPrg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i];
                                }
                            }
                            else
                            {
                                pPrg.GlobalData.Clear();
                            }

                            FilteredDataOffset = NextPrg.FilteredDataOffset;
                            FilteredDataSize = NextPrg.FilteredDataSize;
                            if (FilteredData.Length < FilteredDataSize)
                            {
                                ArrayPool<byte>.Shared.Return(FilteredData);
                                FilteredData = ArrayPool<byte>.Shared.Rent(FilteredDataSize);
                            }
                            for (var i = 0; i < FilteredDataSize; i++)
                            {
                                FilteredData[i] = NextPrg.GlobalData[FilteredDataOffset + i];
                            }

                            I++;
                            prgStack[I] = null;
                        }

                        await writeStream
                            .WriteAsync(FilteredData, 0, FilteredDataSize, cancellationToken)
                            .ConfigureAwait(false);
                        writtenFileSize += FilteredDataSize;
                        destUnpSize -= FilteredDataSize;
                        WrittenBorder = BlockEnd;
                        WriteSize = (unpPtr - WrittenBorder) & PackDef.MAXWINMASK;
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(FilteredData);
                    }
                }
                else
                {
                    for (var J = I; J < prgStack.Count; J++)
                    {
                        var filt = prgStack[J];
                        if (filt != null && filt.NextWindow)
                        {
                            filt.NextWindow = false;
                        }
                    }
                    wrPtr = WrittenBorder;
                    return;
                }
            }
        }

        await UnpWriteAreaAsync(WrittenBorder, unpPtr, cancellationToken).ConfigureAwait(false);
        wrPtr = unpPtr;
    }

    private async Task UnpWriteAreaAsync(
        int startPtr,
        int endPtr,
        CancellationToken cancellationToken = default
    )
    {
        if (endPtr < startPtr)
        {
            await UnpWriteDataAsync(
                    window,
                    startPtr,
                    -startPtr & PackDef.MAXWINMASK,
                    cancellationToken
                )
                .ConfigureAwait(false);
            await UnpWriteDataAsync(window, 0, endPtr, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await UnpWriteDataAsync(window, startPtr, endPtr - startPtr, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task UnpWriteDataAsync(
        byte[] data,
        int offset,
        int size,
        CancellationToken cancellationToken = default
    )
    {
        if (destUnpSize < 0)
        {
            return;
        }
        var writeSize = size;
        if (writeSize > destUnpSize)
        {
            writeSize = (int)destUnpSize;
        }
        await writeStream
            .WriteAsync(data, offset, writeSize, cancellationToken)
            .ConfigureAwait(false);

        writtenFileSize += size;
        destUnpSize -= size;
    }
}
