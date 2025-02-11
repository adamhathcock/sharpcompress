#nullable disable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.PPMd.H;
using SharpCompress.Compressors.Rar.UnpackV1.Decode;
using SharpCompress.Compressors.Rar.UnpackV1.PPM;
using SharpCompress.Compressors.Rar.VM;

namespace SharpCompress.Compressors.Rar.UnpackV1;

internal sealed partial class Unpack : BitInput, IRarUnpack, IDisposable
{
    private readonly BitInput Inp;
    private bool disposed;

    public Unpack() =>
        // to ease in porting Unpack50.cs
        Inp = this;

    public void Dispose()
    {
        if (!disposed)
        {
            if (!externalWindow)
            {
                ArrayPool<byte>.Shared.Return(window);
                window = null;
            }
            disposed = true;
        }
    }

    public bool FileExtracted { get; private set; }

    public long DestSize
    {
        get => destUnpSize;
        set
        {
            destUnpSize = value;
            FileExtracted = false;
        }
    }

    public bool Suspended
    {
        get => suspended;
        set => suspended = value;
    }

    public int Char
    {
        get
        {
            if (inAddr > MAX_SIZE - 30)
            {
                unpReadBuf();
            }
            return (InBuf[inAddr++] & 0xff);
        }
    }

    public int PpmEscChar { get; set; }

    private readonly ModelPpm ppm = new();

    private readonly RarVM rarVM = new();

    // Filters code, one entry per filter
    private readonly List<UnpackFilter> filters = new();

    // Filters stack, several entrances of same filter are possible
    private readonly List<UnpackFilter> prgStack = new();

    // lengths of preceding blocks, one length per filter. Used to reduce size
    // required to write block length if lengths are repeating
    private readonly List<int> oldFilterLengths = new();

    private int lastFilter;

    private bool tablesRead;

    private readonly byte[] unpOldTable = new byte[PackDef.HUFF_TABLE_SIZE];

    private BlockTypes unpBlockType;

    private bool externalWindow;

    private long writtenFileSize;

    private bool ppmError;

    private int prevLowDist;

    private int lowDistRepCount;

    private static readonly int[] DBitLengthCounts =
    {
        4,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        2,
        14,
        0,
        12,
    };

    private FileHeader fileHeader;

    private void Init(byte[] window)
    {
        if (this.window is null && window is null)
        {
            this.window = ArrayPool<byte>.Shared.Rent(PackDef.MAXWINSIZE);
        }
        else if (window is not null)
        {
            this.window = window;
            externalWindow = true;
        }
        inAddr = 0;
        UnpInitData(false);
    }

    public void DoUnpack(FileHeader fileHeader, Stream readStream, Stream writeStream)
    {
        destUnpSize = fileHeader.UncompressedSize;
        this.fileHeader = fileHeader;
        this.readStream = readStream;
        this.writeStream = writeStream;
        if (!fileHeader.IsSolid)
        {
            Init(null);
        }
        suspended = false;
        DoUnpack();
    }

    public void DoUnpack()
    {
        if (fileHeader.CompressionMethod == 0)
        {
            UnstoreFile();
            return;
        }
        switch (fileHeader.CompressionAlgorithm)
        {
            case 15: // rar 1.5 compression
                unpack15(fileHeader.IsSolid);
                break;

            case 20: // rar 2.x compression
            case 26: // files larger than 2GB
                unpack20(fileHeader.IsSolid);
                break;

            case 29: // rar 3.x compression
            case 36: // alternative hash
                Unpack29(fileHeader.IsSolid);
                break;

            case 50: // rar 5.x compression
                Unpack5(fileHeader.IsSolid);
                break;

            default:
                throw new InvalidFormatException(
                    "unknown rar compression version " + fileHeader.CompressionAlgorithm
                );
        }
    }

    private void UnstoreFile()
    {
        Span<byte> buffer = stackalloc byte[(int)Math.Min(0x10000, destUnpSize)];
        do
        {
            var code = readStream.Read(buffer);
            if (code == 0 || code == -1)
            {
                break;
            }
            code = code < destUnpSize ? code : (int)destUnpSize;
            writeStream.Write(buffer.Slice(0, code));
            destUnpSize -= code;
        } while (!suspended && destUnpSize > 0);
    }

    private void Unpack29(bool solid)
    {
        Span<int> DDecode = stackalloc int[PackDef.DC];
        Span<byte> DBits = stackalloc byte[PackDef.DC];

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
            if (!unpReadBuf())
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
                if (!unpReadBuf())
                {
                    break;
                }
            }

            // System.out.println(((wrPtr - unpPtr) &
            // Compress.MAXWINMASK)+":"+wrPtr+":"+unpPtr);
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
                                    // Bug fixed
                                    Length = ch & 0xff;
                                }
                                else
                                {
                                    // Bug fixed
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

    private void UnpWriteBuf()
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
                flt.NextWindow = false; // ->NextWindow=false;
                continue;
            }
            var BlockStart = flt.BlockStart; // ->BlockStart;
            var BlockLength = flt.BlockLength; // ->BlockLength;
            if (((BlockStart - WrittenBorder) & PackDef.MAXWINMASK) < WriteSize)
            {
                if (WrittenBorder != BlockStart)
                {
                    UnpWriteArea(WrittenBorder, BlockStart);
                    WrittenBorder = BlockStart;
                    WriteSize = (unpPtr - WrittenBorder) & PackDef.MAXWINMASK;
                }
                if (BlockLength <= WriteSize)
                {
                    var BlockEnd = (BlockStart + BlockLength) & PackDef.MAXWINMASK;
                    if (BlockStart < BlockEnd || BlockEnd == 0)
                    {
                        // VM.SetMemory(0,Window+BlockStart,BlockLength);
                        rarVM.setMemory(0, window, BlockStart, BlockLength);
                    }
                    else
                    {
                        var FirstPartLength = PackDef.MAXWINSIZE - BlockStart;

                        // VM.SetMemory(0,Window+BlockStart,FirstPartLength);
                        rarVM.setMemory(0, window, BlockStart, FirstPartLength);

                        // VM.SetMemory(FirstPartLength,Window,BlockEnd);
                        rarVM.setMemory(FirstPartLength, window, 0, BlockEnd);
                    }

                    var ParentPrg = filters[flt.ParentFilter].Program;
                    var Prg = flt.Program;

                    if (ParentPrg.GlobalData.Count > RarVM.VM_FIXEDGLOBALSIZE)
                    {
                        // copy global data from previous script execution if
                        // any
                        // Prg->GlobalData.Alloc(ParentPrg->GlobalData.Size());
                        // memcpy(&Prg->GlobalData[VM_FIXEDGLOBALSIZE],&ParentPrg->GlobalData[VM_FIXEDGLOBALSIZE],ParentPrg->GlobalData.Size()-VM_FIXEDGLOBALSIZE);
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
                        // save global data for next script execution
                        if (ParentPrg.GlobalData.Count < Prg.GlobalData.Count)
                        {
                            //ParentPrg.GlobalData.Clear(); // ->GlobalData.Alloc(Prg->GlobalData.Size());
                            ParentPrg.GlobalData.SetSize(Prg.GlobalData.Count);
                        }

                        // memcpy(&ParentPrg->GlobalData[VM_FIXEDGLOBALSIZE],&Prg->GlobalData[VM_FIXEDGLOBALSIZE],Prg->GlobalData.Size()-VM_FIXEDGLOBALSIZE);
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
                    var FilteredData = new byte[FilteredDataSize];

                    for (var i = 0; i < FilteredDataSize; i++)
                    {
                        FilteredData[i] = rarVM.Mem[FilteredDataOffset + i];

                        // Prg.GlobalData.get(FilteredDataOffset
                        // +
                        // i);
                    }

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

                        // apply several filters to same data block

                        rarVM.setMemory(0, FilteredData, 0, FilteredDataSize);

                        // .SetMemory(0,FilteredData,FilteredDataSize);

                        var pPrg = filters[NextFilter.ParentFilter].Program;
                        var NextPrg = NextFilter.Program;

                        if (pPrg.GlobalData.Count > RarVM.VM_FIXEDGLOBALSIZE)
                        {
                            // copy global data from previous script execution
                            // if any
                            // NextPrg->GlobalData.Alloc(ParentPrg->GlobalData.Size());
                            NextPrg.GlobalData.SetSize(pPrg.GlobalData.Count);

                            // memcpy(&NextPrg->GlobalData[VM_FIXEDGLOBALSIZE],&ParentPrg->GlobalData[VM_FIXEDGLOBALSIZE],ParentPrg->GlobalData.Size()-VM_FIXEDGLOBALSIZE);
                            for (
                                var i = 0;
                                i < pPrg.GlobalData.Count - RarVM.VM_FIXEDGLOBALSIZE;
                                i++
                            )
                            {
                                NextPrg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i] = pPrg.GlobalData[
                                    RarVM.VM_FIXEDGLOBALSIZE + i
                                ];
                            }
                        }

                        ExecuteCode(NextPrg);

                        if (NextPrg.GlobalData.Count > RarVM.VM_FIXEDGLOBALSIZE)
                        {
                            // save global data for next script execution
                            if (pPrg.GlobalData.Count < NextPrg.GlobalData.Count)
                            {
                                pPrg.GlobalData.SetSize(NextPrg.GlobalData.Count);
                            }

                            // memcpy(&ParentPrg->GlobalData[VM_FIXEDGLOBALSIZE],&NextPrg->GlobalData[VM_FIXEDGLOBALSIZE],NextPrg->GlobalData.Size()-VM_FIXEDGLOBALSIZE);
                            for (
                                var i = 0;
                                i < NextPrg.GlobalData.Count - RarVM.VM_FIXEDGLOBALSIZE;
                                i++
                            )
                            {
                                pPrg.GlobalData[RarVM.VM_FIXEDGLOBALSIZE + i] = NextPrg.GlobalData[
                                    RarVM.VM_FIXEDGLOBALSIZE + i
                                ];
                            }
                        }
                        else
                        {
                            pPrg.GlobalData.Clear();
                        }
                        FilteredDataOffset = NextPrg.FilteredDataOffset;
                        FilteredDataSize = NextPrg.FilteredDataSize;

                        FilteredData = new byte[FilteredDataSize];
                        for (var i = 0; i < FilteredDataSize; i++)
                        {
                            FilteredData[i] = NextPrg.GlobalData[FilteredDataOffset + i];
                        }

                        I++;
                        prgStack[I] = null;
                    }
                    writeStream.Write(FilteredData, 0, FilteredDataSize);
                    unpSomeRead = true;
                    writtenFileSize += FilteredDataSize;
                    destUnpSize -= FilteredDataSize;
                    WrittenBorder = BlockEnd;
                    WriteSize = (unpPtr - WrittenBorder) & PackDef.MAXWINMASK;
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

        UnpWriteArea(WrittenBorder, unpPtr);
        wrPtr = unpPtr;
    }

    private void UnpWriteArea(int startPtr, int endPtr)
    {
        if (endPtr != startPtr)
        {
            unpSomeRead = true;
        }
        if (endPtr < startPtr)
        {
            UnpWriteData(window, startPtr, -startPtr & PackDef.MAXWINMASK);
            UnpWriteData(window, 0, endPtr);
            unpAllBuf = true;
        }
        else
        {
            UnpWriteData(window, startPtr, endPtr - startPtr);
        }
    }

    private void UnpWriteData(byte[] data, int offset, int size)
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
        writeStream.Write(data, offset, writeSize);

        writtenFileSize += size;
        destUnpSize -= size;
    }

    private void InsertOldDist(uint distance) =>
        // TODO uint
        InsertOldDist((int)distance);

    private void InsertOldDist(int distance)
    {
        oldDist[3] = oldDist[2];
        oldDist[2] = oldDist[1];
        oldDist[1] = oldDist[0];
        oldDist[0] = distance;
    }

    private void InsertLastMatch(int length, int distance)
    {
        lastDist = distance;
        lastLength = length;
    }

    private void CopyString(uint length, uint distance) =>
        // TODO uint
        CopyString((int)length, (int)distance);

    private void CopyString(int length, int distance)
    {
        // System.out.println("copyString(" + length + ", " + distance + ")");

        var destPtr = unpPtr - distance;

        // System.out.println(unpPtr+":"+distance);
        if (destPtr >= 0 && destPtr < PackDef.MAXWINSIZE - 260 && unpPtr < PackDef.MAXWINSIZE - 260)
        {
            window[unpPtr++] = window[destPtr++];

            while (--length > 0)
            {
                window[unpPtr++] = window[destPtr++];
            }
        }
        else
        {
            while (length-- != 0)
            {
                window[unpPtr] = window[destPtr++ & PackDef.MAXWINMASK];
                unpPtr = (unpPtr + 1) & PackDef.MAXWINMASK;
            }
        }
    }

    private void UnpInitData(bool solid)
    {
        if (!solid)
        {
            tablesRead = false;
            new Span<int>(oldDist).Clear(); // memset(oldDist,0,sizeof(OldDist));

            oldDistPtr = 0;
            lastDist = 0;
            lastLength = 0;

            new Span<byte>(unpOldTable).Clear(); // memset(UnpOldTable,0,sizeof(UnpOldTable));

            unpPtr = 0;
            wrPtr = 0;
            PpmEscChar = 2;
            WriteBorder = Math.Min(MaxWinSize, UNPACK_MAX_WRITE) & MaxWinMask;

            InitFilters();
        }
        InitBitInput();
        ppmError = false;
        writtenFileSize = 0;
        readTop = 0;
        readBorder = 0;
        unpInitData20(solid);
    }

    //void Unpack::UnpInitData(bool Solid)
    //{
    //  if (!Solid)
    //  {
    //    memset(OldDist,0,sizeof(OldDist));
    //    OldDistPtr=0;
    //    LastDist=LastLength=0;
    ////    memset(Window,0,MaxWinSize);
    //    memset(&BlockTables,0,sizeof(BlockTables));
    //    UnpPtr=WrPtr=0;
    //    WriteBorder=Min(MaxWinSize,UNPACK_MAX_WRITE)&MaxWinMask;
    //  }
    //  // Filters never share several solid files, so we can safely reset them
    //  // even in solid archive.
    //  InitFilters();
    //
    //  Inp.InitBitInput();
    //  WrittenFileSize=0;
    //  ReadTop=0;
    //  ReadBorder=0;
    //
    //  memset(&BlockHeader,0,sizeof(BlockHeader));
    //  BlockHeader.BlockSize=-1;  // '-1' means not defined yet.
    //#ifndef SFX_MODULE
    //  UnpInitData20(Solid);
    //#endif
    //  UnpInitData30(Solid);
    //  UnpInitData50(Solid);
    //}

    private void InitFilters()
    {
        oldFilterLengths.Clear();
        lastFilter = 0;

        filters.Clear();

        prgStack.Clear();
    }

    private bool ReadEndOfBlock()
    {
        var BitField = GetBits();
        bool NewTable,
            NewFile = false;
        if ((BitField & 0x8000) != 0)
        {
            NewTable = true;
            AddBits(1);
        }
        else
        {
            NewFile = true;
            NewTable = (BitField & 0x4000) != 0;
            AddBits(2);
        }
        tablesRead = !NewTable;
        return !(NewFile || NewTable && !ReadTables());
    }

    private bool ReadTables()
    {
        Span<byte> bitLength = stackalloc byte[PackDef.BC];
        Span<byte> table = stackalloc byte[PackDef.HUFF_TABLE_SIZE];

        if (inAddr > readTop - 25)
        {
            if (!unpReadBuf())
            {
                return (false);
            }
        }
        AddBits((8 - inBit) & 7);
        long bitField = GetBits() & unchecked((int)0xffFFffFF);
        if ((bitField & 0x8000) != 0)
        {
            unpBlockType = BlockTypes.BLOCK_PPM;
            return (ppm.DecodeInit(this, PpmEscChar));
        }
        unpBlockType = BlockTypes.BLOCK_LZ;

        prevLowDist = 0;
        lowDistRepCount = 0;

        if ((bitField & 0x4000) == 0)
        {
            new Span<byte>(unpOldTable).Clear(); // memset(UnpOldTable,0,sizeof(UnpOldTable));
        }
        AddBits(2);

        for (var i = 0; i < PackDef.BC; i++)
        {
            var length = (Utility.URShift(GetBits(), 12)) & 0xFF;
            AddBits(4);
            if (length == 15)
            {
                var zeroCount = (Utility.URShift(GetBits(), 12)) & 0xFF;
                AddBits(4);
                if (zeroCount == 0)
                {
                    bitLength[i] = 15;
                }
                else
                {
                    zeroCount += 2;
                    while (zeroCount-- > 0 && i < bitLength.Length)
                    {
                        bitLength[i++] = 0;
                    }
                    i--;
                }
            }
            else
            {
                bitLength[i] = (byte)length;
            }
        }

        UnpackUtility.makeDecodeTables(bitLength, 0, BD, PackDef.BC);

        var TableSize = PackDef.HUFF_TABLE_SIZE;

        for (var i = 0; i < TableSize; )
        {
            if (inAddr > readTop - 5)
            {
                if (!unpReadBuf())
                {
                    return (false);
                }
            }
            var Number = this.decodeNumber(BD);
            if (Number < 16)
            {
                table[i] = (byte)((Number + unpOldTable[i]) & 0xf);
                i++;
            }
            else if (Number < 18)
            {
                int N;
                if (Number == 16)
                {
                    N = (Utility.URShift(GetBits(), 13)) + 3;
                    AddBits(3);
                }
                else
                {
                    N = (Utility.URShift(GetBits(), 9)) + 11;
                    AddBits(7);
                }
                while (N-- > 0 && i < TableSize)
                {
                    table[i] = table[i - 1];
                    i++;
                }
            }
            else
            {
                int N;
                if (Number == 18)
                {
                    N = (Utility.URShift(GetBits(), 13)) + 3;
                    AddBits(3);
                }
                else
                {
                    N = (Utility.URShift(GetBits(), 9)) + 11;
                    AddBits(7);
                }
                while (N-- > 0 && i < TableSize)
                {
                    table[i++] = 0;
                }
            }
        }
        tablesRead = true;
        if (inAddr > readTop)
        {
            return (false);
        }
        UnpackUtility.makeDecodeTables(table, 0, LD, PackDef.NC);
        UnpackUtility.makeDecodeTables(table, PackDef.NC, DD, PackDef.DC);
        UnpackUtility.makeDecodeTables(table, PackDef.NC + PackDef.DC, LDD, PackDef.LDC);
        UnpackUtility.makeDecodeTables(
            table,
            PackDef.NC + PackDef.DC + PackDef.LDC,
            RD,
            PackDef.RC
        );

        // memcpy(unpOldTable,table,sizeof(unpOldTable));

        table.CopyTo(unpOldTable);
        return (true);
    }

    private bool ReadVMCode()
    {
        var FirstByte = GetBits() >> 8;
        AddBits(8);
        var Length = (FirstByte & 7) + 1;
        if (Length == 7)
        {
            Length = (GetBits() >> 8) + 7;
            AddBits(8);
        }
        else if (Length == 8)
        {
            Length = GetBits();
            AddBits(16);
        }

        var vmCode = new List<byte>();
        for (var I = 0; I < Length; I++)
        {
            if (inAddr >= readTop - 1 && !unpReadBuf() && I < Length - 1)
            {
                return (false);
            }
            vmCode.Add((byte)(GetBits() >> 8));
            AddBits(8);
        }
        return (AddVMCode(FirstByte, vmCode, Length));
    }

    private bool ReadVMCodePPM()
    {
        var FirstByte = ppm.DecodeChar();
        if (FirstByte == -1)
        {
            return (false);
        }
        var Length = (FirstByte & 7) + 1;
        if (Length == 7)
        {
            var B1 = ppm.DecodeChar();
            if (B1 == -1)
            {
                return (false);
            }
            Length = B1 + 7;
        }
        else if (Length == 8)
        {
            var B1 = ppm.DecodeChar();
            if (B1 == -1)
            {
                return (false);
            }
            var B2 = ppm.DecodeChar();
            if (B2 == -1)
            {
                return (false);
            }
            Length = (B1 * 256) + B2;
        }

        var vmCode = new List<byte>();
        for (var I = 0; I < Length; I++)
        {
            var Ch = ppm.DecodeChar();
            if (Ch == -1)
            {
                return (false);
            }
            vmCode.Add((byte)Ch); // VMCode[I]=Ch;
        }
        return (AddVMCode(FirstByte, vmCode, Length));
    }

    private bool AddVMCode(int firstByte, List<byte> vmCode, int length)
    {
        var Inp = new BitInput();
        Inp.InitBitInput();

        // memcpy(Inp.InBuf,Code,Min(BitInput::MAX_SIZE,CodeSize));
        for (var i = 0; i < Math.Min(MAX_SIZE, vmCode.Count); i++)
        {
            Inp.InBuf[i] = vmCode[i];
        }
        rarVM.init();

        int FiltPos;
        if ((firstByte & 0x80) != 0)
        {
            FiltPos = RarVM.ReadData(Inp);
            if (FiltPos == 0)
            {
                InitFilters();
            }
            else
            {
                FiltPos--;
            }
        }
        else
        {
            FiltPos = lastFilter; // use the same filter as last time
        }

        if (FiltPos > filters.Count || FiltPos > oldFilterLengths.Count)
        {
            return (false);
        }
        lastFilter = FiltPos;
        var NewFilter = (FiltPos == filters.Count);

        var StackFilter = new UnpackFilter(); // new filter for

        // PrgStack

        UnpackFilter Filter;
        if (NewFilter)
        // new filter code, never used before since VM reset
        {
            // too many different filters, corrupt archive
            if (FiltPos > 1024)
            {
                return (false);
            }

            // Filters[Filters.Size()-1]=Filter=new UnpackFilter;
            Filter = new UnpackFilter();
            filters.Add(Filter);
            StackFilter.ParentFilter = filters.Count - 1;
            oldFilterLengths.Add(0);
            Filter.ExecCount = 0;
        }
        // filter was used in the past
        else
        {
            Filter = filters[FiltPos];
            StackFilter.ParentFilter = FiltPos;
            Filter.ExecCount++; // ->ExecCount++;
        }

        prgStack.Add(StackFilter);
        StackFilter.ExecCount = Filter.ExecCount; // ->ExecCount;

        var BlockStart = RarVM.ReadData(Inp);
        if ((firstByte & 0x40) != 0)
        {
            BlockStart += 258;
        }
        StackFilter.BlockStart = ((BlockStart + unpPtr) & PackDef.MAXWINMASK);
        if ((firstByte & 0x20) != 0)
        {
            StackFilter.BlockLength = RarVM.ReadData(Inp);
        }
        else
        {
            StackFilter.BlockLength =
                FiltPos < oldFilterLengths.Count ? oldFilterLengths[FiltPos] : 0;
        }
        StackFilter.NextWindow =
            (wrPtr != unpPtr) && ((wrPtr - unpPtr) & PackDef.MAXWINMASK) <= BlockStart;

        // DebugLog("\nNextWindow: UnpPtr=%08x WrPtr=%08x
        // BlockStart=%08x",UnpPtr,WrPtr,BlockStart);

        oldFilterLengths[FiltPos] = StackFilter.BlockLength;

        // memset(StackFilter->Prg.InitR,0,sizeof(StackFilter->Prg.InitR));
        new Span<int>(StackFilter.Program.InitR).Clear();
        StackFilter.Program.InitR[3] = RarVM.VM_GLOBALMEMADDR; // StackFilter->Prg.InitR[3]=VM_GLOBALMEMADDR;
        StackFilter.Program.InitR[4] = StackFilter.BlockLength;

        // StackFilter->Prg.InitR[4]=StackFilter->BlockLength;
        StackFilter.Program.InitR[5] = StackFilter.ExecCount; // StackFilter->Prg.InitR[5]=StackFilter->ExecCount;

        if ((firstByte & 0x10) != 0)
        // set registers to optional parameters
        // if any
        {
            var InitMask = Utility.URShift(Inp.GetBits(), 9);
            Inp.AddBits(7);
            for (var I = 0; I < 7; I++)
            {
                if ((InitMask & (1 << I)) != 0)
                {
                    // StackFilter->Prg.InitR[I]=RarVM::ReadData(Inp);
                    StackFilter.Program.InitR[I] = RarVM.ReadData(Inp);
                }
            }
        }

        if (NewFilter)
        {
            var VMCodeSize = RarVM.ReadData(Inp);
            if (VMCodeSize >= 0x10000 || VMCodeSize == 0)
            {
                return (false);
            }
            Span<byte> VMCode = stackalloc byte[VMCodeSize];
            for (var I = 0; I < VMCodeSize; I++)
            {
                if (Inp.Overflow(3))
                {
                    return (false);
                }
                VMCode[I] = (byte)(Inp.GetBits() >> 8);
                Inp.AddBits(8);
            }

            // VM.Prepare(&VMCode[0],VMCodeSize,&Filter->Prg);
            rarVM.prepare(VMCode, VMCodeSize, Filter.Program);
        }
        StackFilter.Program.AltCommands = Filter.Program.Commands; // StackFilter->Prg.AltCmd=&Filter->Prg.Cmd[0];
        StackFilter.Program.CommandCount = Filter.Program.CommandCount;

        // StackFilter->Prg.CmdCount=Filter->Prg.CmdCount;

        var StaticDataSize = Filter.Program.StaticData.Count;
        if (StaticDataSize > 0 && StaticDataSize < RarVM.VM_GLOBALMEMSIZE)
        {
            // read statically defined data contained in DB commands
            // StackFilter->Prg.StaticData.Add(StaticDataSize);
            StackFilter.Program.StaticData = Filter.Program.StaticData;

            // memcpy(&StackFilter->Prg.StaticData[0],&Filter->Prg.StaticData[0],StaticDataSize);
        }

        if (StackFilter.Program.GlobalData.Count < RarVM.VM_FIXEDGLOBALSIZE)
        {
            // StackFilter->Prg.GlobalData.Reset();
            // StackFilter->Prg.GlobalData.Add(VM_FIXEDGLOBALSIZE);
            StackFilter.Program.GlobalData.Clear();
            StackFilter.Program.GlobalData.SetSize(RarVM.VM_FIXEDGLOBALSIZE);
        }

        // byte *GlobalData=&StackFilter->Prg.GlobalData[0];

        var globalData = StackFilter.Program.GlobalData;
        for (var I = 0; I < 7; I++)
        {
            rarVM.SetLowEndianValue(globalData, I * 4, StackFilter.Program.InitR[I]);
        }

        // VM.SetLowEndianValue((uint
        // *)&GlobalData[0x1c],StackFilter->BlockLength);
        rarVM.SetLowEndianValue(globalData, 0x1c, StackFilter.BlockLength);

        // VM.SetLowEndianValue((uint *)&GlobalData[0x20],0);
        rarVM.SetLowEndianValue(globalData, 0x20, 0);
        rarVM.SetLowEndianValue(globalData, 0x24, 0);
        rarVM.SetLowEndianValue(globalData, 0x28, 0);

        // VM.SetLowEndianValue((uint
        // *)&GlobalData[0x2c],StackFilter->ExecCount);
        rarVM.SetLowEndianValue(globalData, 0x2c, StackFilter.ExecCount);

        // memset(&GlobalData[0x30],0,16);
        for (var i = 0; i < 16; i++)
        {
            globalData[0x30 + i] = 0x0;
        }
        if ((firstByte & 8) != 0)
        // put data block passed as parameter if any
        {
            if (Inp.Overflow(3))
            {
                return (false);
            }
            var DataSize = RarVM.ReadData(Inp);
            if (DataSize > RarVM.VM_GLOBALMEMSIZE - RarVM.VM_FIXEDGLOBALSIZE)
            {
                return (false);
            }
            var CurSize = StackFilter.Program.GlobalData.Count;
            if (CurSize < DataSize + RarVM.VM_FIXEDGLOBALSIZE)
            {
                // StackFilter->Prg.GlobalData.Add(DataSize+VM_FIXEDGLOBALSIZE-CurSize);
                StackFilter.Program.GlobalData.SetSize(
                    DataSize + RarVM.VM_FIXEDGLOBALSIZE - CurSize
                );
            }
            var offset = RarVM.VM_FIXEDGLOBALSIZE;
            globalData = StackFilter.Program.GlobalData;
            for (var I = 0; I < DataSize; I++)
            {
                if (Inp.Overflow(3))
                {
                    return (false);
                }
                globalData[offset + I] = (byte)(Utility.URShift(Inp.GetBits(), 8));
                Inp.AddBits(8);
            }
        }
        return (true);
    }

    private void ExecuteCode(VMPreparedProgram Prg)
    {
        if (Prg.GlobalData.Count > 0)
        {
            // Prg->InitR[6]=int64to32(WrittenFileSize);
            Prg.InitR[6] = (int)(writtenFileSize);

            // rarVM.SetLowEndianValue((uint
            // *)&Prg->GlobalData[0x24],int64to32(WrittenFileSize));
            rarVM.SetLowEndianValue(Prg.GlobalData, 0x24, (int)writtenFileSize);

            // rarVM.SetLowEndianValue((uint
            // *)&Prg->GlobalData[0x28],int64to32(WrittenFileSize>>32));
            rarVM.SetLowEndianValue(
                Prg.GlobalData,
                0x28,
                (int)(Utility.URShift(writtenFileSize, 32))
            );
            rarVM.execute(Prg);
        }
    }

    private void CleanUp()
    {
        if (ppm != null)
        {
            var allocator = ppm.SubAlloc;
            allocator?.StopSubAllocator();
        }
    }
}
