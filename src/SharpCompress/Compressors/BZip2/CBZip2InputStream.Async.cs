#nullable disable

using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Compressors.BZip2;

internal partial class CBZip2InputStream
{
    public async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        if (streamEnd)
        {
            return -1;
        }
        var retChar = currentChar;
        switch (currentState)
        {
            case START_BLOCK_STATE:
                break;
            case RAND_PART_A_STATE:
                break;
            case RAND_PART_B_STATE:
                await SetupRandPartBAsync(cancellationToken).ConfigureAwait(false);
                break;
            case RAND_PART_C_STATE:
                await SetupRandPartCAsync(cancellationToken).ConfigureAwait(false);
                break;
            case NO_RAND_PART_A_STATE:
                break;
            case NO_RAND_PART_B_STATE:
                await SetupNoRandPartBAsync(cancellationToken).ConfigureAwait(false);
                break;
            case NO_RAND_PART_C_STATE:
                await SetupNoRandPartCAsync(cancellationToken).ConfigureAwait(false);
                break;
            default:
                break;
        }
        return retChar;
    }

    private async ValueTask<bool> InitializeAsync(
        bool isFirstStream,
        CancellationToken cancellationToken
    )
    {
        var singleByte = new byte[1];
        var read0 = await bsStream
            .ReadAsync(singleByte, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        var magic0 = read0 == 0 ? -1 : singleByte[0];
        var read1 = await bsStream
            .ReadAsync(singleByte, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        var magic1 = read1 == 0 ? -1 : singleByte[0];
        var read2 = await bsStream
            .ReadAsync(singleByte, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        var magic2 = read2 == 0 ? -1 : singleByte[0];
        if (magic0 == -1 && !isFirstStream)
        {
            return false;
        }
        if (magic0 != 'B' || magic1 != 'Z' || magic2 != 'h')
        {
            throw new InvalidFormatException("Not a BZIP2 marked stream");
        }
        var read3 = await bsStream
            .ReadAsync(singleByte, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        var magic3 = read3 == 0 ? -1 : singleByte[0];
        if (magic3 < '1' || magic3 > '9')
        {
            BsFinishedWithStream();
            streamEnd = true;
            return false;
        }

        SetDecompressStructureSizes(magic3 - '0');
        bsLive = 0;
        computedCombinedCRC = 0;
        return true;
    }

    private async ValueTask InitBlockAsync(CancellationToken cancellationToken)
    {
        char magic1,
            magic2,
            magic3,
            magic4;
        char magic5,
            magic6;

        while (true)
        {
            magic1 = await BsGetUCharAsync(cancellationToken).ConfigureAwait(false);
            magic2 = await BsGetUCharAsync(cancellationToken).ConfigureAwait(false);
            magic3 = await BsGetUCharAsync(cancellationToken).ConfigureAwait(false);
            magic4 = await BsGetUCharAsync(cancellationToken).ConfigureAwait(false);
            magic5 = await BsGetUCharAsync(cancellationToken).ConfigureAwait(false);
            magic6 = await BsGetUCharAsync(cancellationToken).ConfigureAwait(false);
            if (
                magic1 != 0x17
                || magic2 != 0x72
                || magic3 != 0x45
                || magic4 != 0x38
                || magic5 != 0x50
                || magic6 != 0x90
            )
            {
                break;
            }

            if (await CompleteAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        if (
            magic1 != 0x31
            || magic2 != 0x41
            || magic3 != 0x59
            || magic4 != 0x26
            || magic5 != 0x53
            || magic6 != 0x59
        )
        {
            BadBlockHeader();
            streamEnd = true;
            return;
        }

        storedBlockCRC = await BsGetInt32Async(cancellationToken).ConfigureAwait(false);

        if (await BsRAsync(1, cancellationToken).ConfigureAwait(false) == 1)
        {
            blockRandomised = true;
        }
        else
        {
            blockRandomised = false;
        }

        //        currBlockNo++;
        await GetAndMoveToFrontDecodeAsync(cancellationToken).ConfigureAwait(false);

        mCrc.InitialiseCRC();
        currentState = START_BLOCK_STATE;
    }

    private async ValueTask<bool> CompleteAsync(CancellationToken cancellationToken)
    {
        storedCombinedCRC = await BsGetInt32Async(cancellationToken).ConfigureAwait(false);
        if (storedCombinedCRC != computedCombinedCRC)
        {
            CrcError();
        }

        var complete =
            !decompressConcatenated
            || !(await InitializeAsync(false, cancellationToken).ConfigureAwait(false));
        if (complete)
        {
            BsFinishedWithStream();
            streamEnd = true;
        }

        // Look for the next .bz2 stream if decompressing
        // concatenated files.
        return complete;
    }

    private async ValueTask<int> BsGetintAsync(CancellationToken cancellationToken)
    {
        var u = 0;
        u = (u << 8) | (await BsRAsync(8, cancellationToken).ConfigureAwait(false));
        u = (u << 8) | (await BsRAsync(8, cancellationToken).ConfigureAwait(false));
        u = (u << 8) | (await BsRAsync(8, cancellationToken).ConfigureAwait(false));
        u = (u << 8) | (await BsRAsync(8, cancellationToken).ConfigureAwait(false));
        return u;
    }

    private async ValueTask RecvDecodingTablesAsync(CancellationToken cancellationToken)
    {
        var len = InitCharArray(BZip2Constants.N_GROUPS, BZip2Constants.MAX_ALPHA_SIZE);
        int i,
            j,
            t,
            nGroups,
            nSelectors,
            alphaSize;
        int minLen,
            maxLen;
        var inUse16 = new bool[16];

        /* Receive the mapping table */
        for (i = 0; i < 16; i++)
        {
            if (await BsRAsync(1, cancellationToken).ConfigureAwait(false) == 1)
            {
                inUse16[i] = true;
            }
            else
            {
                inUse16[i] = false;
            }
        }

        for (i = 0; i < 256; i++)
        {
            inUse[i] = false;
        }

        for (i = 0; i < 16; i++)
        {
            if (inUse16[i])
            {
                for (j = 0; j < 16; j++)
                {
                    if (await BsRAsync(1, cancellationToken).ConfigureAwait(false) == 1)
                    {
                        inUse[(i * 16) + j] = true;
                    }
                }
            }
        }

        MakeMaps();
        alphaSize = nInUse + 2;

        /* Now the selectors */
        nGroups = await BsRAsync(3, cancellationToken).ConfigureAwait(false);
        nSelectors = await BsRAsync(15, cancellationToken).ConfigureAwait(false);
        for (i = 0; i < nSelectors; i++)
        {
            j = 0;
            while (await BsRAsync(1, cancellationToken).ConfigureAwait(false) == 1)
            {
                j++;
            }
            if (i < BZip2Constants.MAX_SELECTORS)
            {
                selectorMtf[i] = (char)j;
            }
        }

        nSelectors = Math.Min(nSelectors, BZip2Constants.MAX_SELECTORS);

        /* Undo the MTF values for the selectors. */
        {
            var pos = new char[BZip2Constants.N_GROUPS];
            char tmp,
                v;
            for (v = '\0'; v < nGroups; v++)
            {
                pos[v] = v;
            }

            for (i = 0; i < nSelectors; i++)
            {
                v = selectorMtf[i];
                tmp = pos[v];
                while (v > 0)
                {
                    pos[v] = pos[v - 1];
                    v--;
                }
                pos[0] = tmp;
                selector[i] = tmp;
            }
        }

        /* Now the coding tables */
        for (t = 0; t < nGroups; t++)
        {
            var curr = await BsRAsync(5, cancellationToken).ConfigureAwait(false);
            for (i = 0; i < alphaSize; i++)
            {
                while (await BsRAsync(1, cancellationToken).ConfigureAwait(false) == 1)
                {
                    if (await BsRAsync(1, cancellationToken).ConfigureAwait(false) == 0)
                    {
                        curr++;
                    }
                    else
                    {
                        curr--;
                    }
                }
                len[t][i] = (char)curr;
            }
        }

        /* Create the Huffman decoding tables */
        for (t = 0; t < nGroups; t++)
        {
            minLen = 32;
            maxLen = 0;
            for (i = 0; i < alphaSize; i++)
            {
                if (len[t][i] > maxLen)
                {
                    maxLen = len[t][i];
                }
                if (len[t][i] < minLen)
                {
                    minLen = len[t][i];
                }
            }
            HbCreateDecodeTables(limit[t], basev[t], perm[t], len[t], minLen, maxLen, alphaSize);
            minLens[t] = minLen;
        }
    }

    private async ValueTask GetAndMoveToFrontDecodeAsync(CancellationToken cancellationToken)
    {
        var yy = new char[256];
        int i,
            j,
            nextSym,
            limitLast;
        int EOB,
            groupNo,
            groupPos;
        var singleByte = new byte[1];

        limitLast = BZip2Constants.baseBlockSize * blockSize100k;
        origPtr = await BsGetIntVSAsync(24, cancellationToken).ConfigureAwait(false);

        await RecvDecodingTablesAsync(cancellationToken).ConfigureAwait(false);
        EOB = nInUse + 1;
        groupNo = -1;
        groupPos = 0;

        /*
        Setting up the unzftab entries here is not strictly
        necessary, but it does save having to do it later
        in a separate pass, and so saves a block's worth of
        cache misses.
        */
        for (i = 0; i <= 255; i++)
        {
            unzftab[i] = 0;
        }

        for (i = 0; i <= 255; i++)
        {
            yy[i] = (char)i;
        }

        last = -1;

        {
            int zt,
                zn,
                zvec,
                zj;
            if (groupPos == 0)
            {
                groupNo++;
                groupPos = BZip2Constants.G_SIZE;
            }
            groupPos--;
            zt = selector[groupNo];
            zn = minLens[zt];
            zvec = await BsRAsync(zn, cancellationToken).ConfigureAwait(false);
            while (zvec > limit[zt][zn])
            {
                zn++;
                {
                    {
                        while (bsLive < 1)
                        {
                            int zzi;
                            int thech = '\0';
                            try
                            {
                                var readCount = await bsStream
                                    .ReadAsync(singleByte, 0, 1, cancellationToken)
                                    .ConfigureAwait(false);
                                thech = readCount == 0 ? '\uffff' : singleByte[0];
                            }
                            catch (IOException)
                            {
                                CompressedStreamEOF();
                            }
                            if (thech == '\uffff')
                            {
                                CompressedStreamEOF();
                            }
                            zzi = thech;
                            bsBuff = (bsBuff << 8) | (zzi & 0xff);
                            bsLive += 8;
                        }
                    }
                    zj = (bsBuff >> (bsLive - 1)) & 1;
                    bsLive--;
                }
                zvec = (zvec << 1) | zj;
            }
            nextSym = perm[zt][zvec - basev[zt][zn]];
        }

        while (true)
        {
            if (nextSym == EOB)
            {
                break;
            }

            if (nextSym == BZip2Constants.RUNA || nextSym == BZip2Constants.RUNB)
            {
                char ch;
                var s = -1;
                var N = 1;
                do
                {
                    if (nextSym == BZip2Constants.RUNA)
                    {
                        s += (0 + 1) * N;
                    }
                    else if (nextSym == BZip2Constants.RUNB)
                    {
                        s += (1 + 1) * N;
                    }
                    N *= 2;
                    {
                        int zt,
                            zn,
                            zvec,
                            zj;
                        if (groupPos == 0)
                        {
                            groupNo++;
                            groupPos = BZip2Constants.G_SIZE;
                        }
                        groupPos--;
                        zt = selector[groupNo];
                        zn = minLens[zt];
                        zvec = await BsRAsync(zn, cancellationToken).ConfigureAwait(false);
                        while (zvec > limit[zt][zn])
                        {
                            zn++;
                            {
                                {
                                    while (bsLive < 1)
                                    {
                                        int zzi;
                                        int thech = '\0';
                                        try
                                        {
                                            var readCount = await bsStream
                                                .ReadAsync(singleByte, 0, 1, cancellationToken)
                                                .ConfigureAwait(false);
                                            thech = readCount == 0 ? '\uffff' : singleByte[0];
                                        }
                                        catch (IOException)
                                        {
                                            CompressedStreamEOF();
                                        }
                                        if (thech == '\uffff')
                                        {
                                            CompressedStreamEOF();
                                        }
                                        zzi = thech;
                                        bsBuff = (bsBuff << 8) | (zzi & 0xff);
                                        bsLive += 8;
                                    }
                                }
                                zj = (bsBuff >> (bsLive - 1)) & 1;
                                bsLive--;
                            }
                            zvec = (zvec << 1) | zj;
                        }
                        nextSym = perm[zt][zvec - basev[zt][zn]];
                    }
                } while (nextSym == BZip2Constants.RUNA || nextSym == BZip2Constants.RUNB);

                s++;
                ch = seqToUnseq[yy[0]];
                unzftab[ch] += s;

                while (s > 0)
                {
                    last++;
                    ll8[last] = ch;
                    s--;
                }

                if (last >= limitLast)
                {
                    BlockOverrun();
                }
            }
            else
            {
                char tmp;
                last++;
                if (last >= limitLast)
                {
                    BlockOverrun();
                }

                tmp = yy[nextSym - 1];
                unzftab[seqToUnseq[tmp]]++;
                ll8[last] = seqToUnseq[tmp];

                /*
                This loop is hammered during decompression,
                hence the unrolling.

                for (j = nextSym-1; j > 0; j--) yy[j] = yy[j-1];
                */

                j = nextSym - 1;
                for (; j > 3; j -= 4)
                {
                    yy[j] = yy[j - 1];
                    yy[j - 1] = yy[j - 2];
                    yy[j - 2] = yy[j - 3];
                    yy[j - 3] = yy[j - 4];
                }
                for (; j > 0; j--)
                {
                    yy[j] = yy[j - 1];
                }

                yy[0] = tmp;
                {
                    int zt,
                        zn,
                        zvec,
                        zj;
                    if (groupPos == 0)
                    {
                        groupNo++;
                        groupPos = BZip2Constants.G_SIZE;
                    }
                    groupPos--;
                    zt = selector[groupNo];
                    zn = minLens[zt];
                    zvec = await BsRAsync(zn, cancellationToken).ConfigureAwait(false);
                    while (zvec > limit[zt][zn])
                    {
                        zn++;
                        {
                            {
                                while (bsLive < 1)
                                {
                                    int zzi;
                                    int thech = '\0';
                                    try
                                    {
                                        var readCount = await bsStream
                                            .ReadAsync(singleByte, 0, 1, cancellationToken)
                                            .ConfigureAwait(false);
                                        thech = readCount == 0 ? '\uffff' : singleByte[0];
                                    }
                                    catch (IOException)
                                    {
                                        CompressedStreamEOF();
                                    }
                                    if (thech == '\uffff')
                                    {
                                        CompressedStreamEOF();
                                    }
                                    zzi = thech;
                                    bsBuff = (bsBuff << 8) | (zzi & 0xff);
                                    bsLive += 8;
                                }
                            }
                            zj = (bsBuff >> (bsLive - 1)) & 1;
                            bsLive--;
                        }
                        zvec = (zvec << 1) | zj;
                    }
                    nextSym = perm[zt][zvec - basev[zt][zn]];
                }
            }
        }
    }

    private async ValueTask SetupBlockAsync(CancellationToken cancellationToken)
    {
        Span<int> cftab = stackalloc int[257];
        char ch;

        cftab[0] = 0;
        for (i = 1; i <= 256; i++)
        {
            cftab[i] = unzftab[i - 1];
        }
        for (i = 1; i <= 256; i++)
        {
            cftab[i] += cftab[i - 1];
        }

        for (i = 0; i <= last; i++)
        {
            ch = ll8[i];
            tt[cftab[ch]] = i;
            cftab[ch]++;
        }

        tPos = tt[origPtr];

        count = 0;
        i2 = 0;
        ch2 = 256; /* not a char and not EOF */

        if (blockRandomised)
        {
            rNToGo = 0;
            rTPos = 0;
            await SetupRandPartAAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            SetupNoRandPartA();
        }
    }

    private async ValueTask SetupRandPartAAsync(CancellationToken cancellationToken)
    {
        if (i2 <= last)
        {
            chPrev = ch2;
            ch2 = ll8[tPos];
            tPos = tt[tPos];
            if (rNToGo == 0)
            {
                rNToGo = BZip2Constants.rNums[rTPos];
                rTPos++;
                if (rTPos == 512)
                {
                    rTPos = 0;
                }
            }
            rNToGo--;
            ch2 ^= (rNToGo == 1) ? (char)1 : (char)0;
            i2++;

            currentChar = ch2;
            currentState = RAND_PART_B_STATE;
            mCrc.UpdateCRC(ch2);
        }
        else
        {
            EndBlock();
            await InitBlockAsync(cancellationToken).ConfigureAwait(false);
            await SetupBlockAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SetupNoRandPartAAsync(CancellationToken cancellationToken)
    {
        if (i2 <= last)
        {
            chPrev = ch2;
            ch2 = ll8[tPos];
            tPos = tt[tPos];
            i2++;

            currentChar = ch2;
            currentState = NO_RAND_PART_B_STATE;
            mCrc.UpdateCRC(ch2);
        }
        else
        {
            EndBlock();
            await InitBlockAsync(cancellationToken).ConfigureAwait(false);
            await SetupBlockAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SetupRandPartBAsync(CancellationToken cancellationToken)
    {
        if (ch2 != chPrev)
        {
            currentState = RAND_PART_A_STATE;
            count = 1;
            await SetupRandPartAAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            count++;
            if (count >= 4)
            {
                z = ll8[tPos];
                tPos = tt[tPos];
                if (rNToGo == 0)
                {
                    rNToGo = BZip2Constants.rNums[rTPos];
                    rTPos++;
                    if (rTPos == 512)
                    {
                        rTPos = 0;
                    }
                }
                rNToGo--;
                z ^= (char)((rNToGo == 1) ? 1 : 0);
                j2 = 0;
                currentState = RAND_PART_C_STATE;
                SetupRandPartC();
            }
            else
            {
                currentState = RAND_PART_A_STATE;
                await SetupRandPartAAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask SetupRandPartCAsync(CancellationToken cancellationToken)
    {
        if (j2 < z)
        {
            currentChar = ch2;
            mCrc.UpdateCRC(ch2);
            j2++;
        }
        else
        {
            currentState = RAND_PART_A_STATE;
            i2++;
            count = 0;
            await SetupRandPartAAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask SetupNoRandPartBAsync(CancellationToken cancellationToken)
    {
        if (ch2 != chPrev)
        {
            currentState = NO_RAND_PART_A_STATE;
            count = 1;
            await SetupNoRandPartAAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            count++;
            if (count >= 4)
            {
                z = ll8[tPos];
                tPos = tt[tPos];
                currentState = NO_RAND_PART_C_STATE;
                j2 = 0;
                await SetupNoRandPartCAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                currentState = NO_RAND_PART_A_STATE;
                await SetupNoRandPartAAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask SetupNoRandPartCAsync(CancellationToken cancellationToken)
    {
        if (j2 < z)
        {
            currentChar = ch2;
            mCrc.UpdateCRC(ch2);
            j2++;
        }
        else
        {
            currentState = NO_RAND_PART_A_STATE;
            i2++;
            count = 0;
            await SetupNoRandPartAAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        var c = -1;
        int k;
        for (k = 0; k < count; ++k)
        {
            cancellationToken.ThrowIfCancellationRequested();
            c = await ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (c == -1)
            {
                break;
            }
            buffer[k + offset] = (byte)c;
        }
        return k;
    }

    private async ValueTask<int> BsRAsync(int n, CancellationToken cancellationToken)
    {
        int v;
        while (bsLive < n)
        {
            int zzi;
            int thech = '\0';
            var b = ArrayPool<byte>.Shared.Rent(1);
            try
            {
                await bsStream.ReadExactAsync(b, 0, 1, cancellationToken).ConfigureAwait(false);
                thech = (char)b[0];
            }
            catch (IOException)
            {
                CompressedStreamEOF();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(b);
            }
            if (thech == '\uffff')
            {
                CompressedStreamEOF();
            }
            zzi = thech;
            bsBuff = (bsBuff << 8) | (zzi & 0xff);
            bsLive += 8;
        }

        v = (bsBuff >> (bsLive - n)) & ((1 << n) - 1);
        bsLive -= n;
        return v;
    }

    private async ValueTask<char> BsGetUCharAsync(CancellationToken cancellationToken) =>
        (char)await BsRAsync(8, cancellationToken).ConfigureAwait(false);

    private async ValueTask<int> BsGetIntVSAsync(
        int numBits,
        CancellationToken cancellationToken
    ) => await BsRAsync(numBits, cancellationToken).ConfigureAwait(false);

    private async ValueTask<int> BsGetInt32Async(CancellationToken cancellationToken) =>
        await BsGetintAsync(cancellationToken).ConfigureAwait(false);

    public static async ValueTask<CBZip2InputStream> CreateAsync(
        Stream zStream,
        bool decompressConcatenated,
        bool leaveOpen = false,
        CancellationToken cancellationToken = default
    )
    {
        var cbZip2InputStream = new CBZip2InputStream(decompressConcatenated, leaveOpen);
        cbZip2InputStream.ll8 = null;
        cbZip2InputStream.tt = null;
        cbZip2InputStream.BsSetStream(zStream);
        await cbZip2InputStream.InitializeAsync(true, cancellationToken).ConfigureAwait(false);
        await cbZip2InputStream.InitBlockAsync(cancellationToken).ConfigureAwait(false);
        await cbZip2InputStream.SetupBlockAsync(cancellationToken).ConfigureAwait(false);
        return cbZip2InputStream;
    }
}
