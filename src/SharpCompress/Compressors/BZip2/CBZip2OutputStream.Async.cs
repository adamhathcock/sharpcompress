using System;
using System.Threading;
using System.Threading.Tasks;

#nullable disable

namespace SharpCompress.Compressors.BZip2;

internal sealed partial class CBZip2OutputStream
{
    private readonly byte[] bsAsyncWriteBuffer = new byte[1];

    /// <summary>
    /// Ensures the BZip2 stream header ('B', 'Z', 'h', blocksize) has been written
    /// asynchronously before the first compressed byte is written.
    /// </summary>
    private async ValueTask EnsureStreamHeaderWrittenAsync(CancellationToken cancellationToken)
    {
        if (!_streamHeaderWritten)
        {
            _streamHeaderWritten = true;
            // Write 'B', 'Z', 'h' async, then set up bit buffer as Initialize() would.
            // Initialize() calls BsPutUChar('h') then BsPutUChar('0'+N):
            //   - First call buffers 'h' (bsLive=8)
            //   - Second call flushes 'h' to stream, then buffers '0'+N (bsLive=8)
            // So after Initialize(), stream has 'h' and bit buffer has '0'+N with bsLive=8.
            var header = new byte[] { (byte)'B', (byte)'Z', (byte)'h' };
            await bsStream
                .WriteAsync(header, 0, header.Length, cancellationToken)
                .ConfigureAwait(false);
            // Replicate the bit buffer state that Initialize() leaves:
            bytesOut = 1; // 'h' was written via BsW (Initialize increments bytesOut via BsW)
            nBlocksRandomised = 0;
            combinedCRC = 0;
            bsBuff = (blockSize100k + '0') << 24;
            bsLive = 8;
            InitBlock();
        }
    }

    public async ValueTask WriteByteAsync(byte bv, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureStreamHeaderWrittenAsync(cancellationToken).ConfigureAwait(false);
        var b = (256 + bv) % 256;
        if (currentChar != -1)
        {
            if (currentChar == b)
            {
                runLength++;
                if (runLength > 254)
                {
                    await WriteRunAsync(cancellationToken).ConfigureAwait(false);
                    currentChar = -1;
                    runLength = 0;
                }
            }
            else
            {
                await WriteRunAsync(cancellationToken).ConfigureAwait(false);
                runLength = 1;
                currentChar = b;
            }
        }
        else
        {
            currentChar = b;
            runLength++;
        }
    }

    private async ValueTask WriteRunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (last < allowableBlockSize)
        {
            inUse[currentChar] = true;
            for (var i = 0; i < runLength; i++)
            {
                mCrc.UpdateCRC((char)currentChar);
            }
            switch (runLength)
            {
                case 1:
                    last++;
                    block[last + 1] = (char)currentChar;
                    break;
                case 2:
                    last++;
                    block[last + 1] = (char)currentChar;
                    last++;
                    block[last + 1] = (char)currentChar;
                    break;
                case 3:
                    last++;
                    block[last + 1] = (char)currentChar;
                    last++;
                    block[last + 1] = (char)currentChar;
                    last++;
                    block[last + 1] = (char)currentChar;
                    break;
                default:
                    inUse[runLength - 4] = true;
                    last++;
                    block[last + 1] = (char)currentChar;
                    last++;
                    block[last + 1] = (char)currentChar;
                    last++;
                    block[last + 1] = (char)currentChar;
                    last++;
                    block[last + 1] = (char)currentChar;
                    last++;
                    block[last + 1] = (char)(runLength - 4);
                    break;
            }
        }
        else
        {
            await EndBlockAsync(cancellationToken).ConfigureAwait(false);
            InitBlock();
            await WriteRunAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask EndBlockAsync(CancellationToken cancellationToken)
    {
        // Skip block processing for empty input (no data written)
        if (last < 0)
        {
            return;
        }

        blockCRC = mCrc.GetFinalCRC();
        combinedCRC = (combinedCRC << 1) | (int)(((uint)combinedCRC) >> 31);
        combinedCRC ^= blockCRC;

        /* sort the block and establish posn of original string */
        DoReversibleTransformation();

        await BsPutUCharAsync(0x31, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x41, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x59, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x26, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x53, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x59, cancellationToken).ConfigureAwait(false);

        await BsPutintAsync(blockCRC, cancellationToken).ConfigureAwait(false);

        if (blockRandomised)
        {
            await BsWAsync(1, 1, cancellationToken).ConfigureAwait(false);
            nBlocksRandomised++;
        }
        else
        {
            await BsWAsync(1, 0, cancellationToken).ConfigureAwait(false);
        }

        await MoveToFrontCodeAndSendAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EndCompressionAsync(CancellationToken cancellationToken)
    {
        await BsPutUCharAsync(0x17, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x72, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x45, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x38, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x50, cancellationToken).ConfigureAwait(false);
        await BsPutUCharAsync(0x90, cancellationToken).ConfigureAwait(false);

        await BsPutintAsync(combinedCRC, cancellationToken).ConfigureAwait(false);

        await BsFinishedWithStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask BsFinishedWithStreamAsync(CancellationToken cancellationToken)
    {
        while (bsLive > 0)
        {
            var ch = bsBuff >> 24;
            bsAsyncWriteBuffer[0] = (byte)ch;
            await bsStream
                .WriteAsync(bsAsyncWriteBuffer, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            bsBuff <<= 8;
            bsLive -= 8;
            bytesOut++;
        }
    }

    private async ValueTask BsWAsync(int n, int v, CancellationToken cancellationToken)
    {
        while (bsLive >= 8)
        {
            var ch = bsBuff >> 24;
            bsAsyncWriteBuffer[0] = (byte)ch;
            await bsStream
                .WriteAsync(bsAsyncWriteBuffer, 0, 1, cancellationToken)
                .ConfigureAwait(false);
            bsBuff <<= 8;
            bsLive -= 8;
            bytesOut++;
        }
        bsBuff |= v << (32 - bsLive - n);
        bsLive += n;
    }

    private ValueTask BsPutUCharAsync(int c, CancellationToken cancellationToken) =>
        BsWAsync(8, c, cancellationToken);

    private async ValueTask BsPutintAsync(int u, CancellationToken cancellationToken)
    {
        await BsWAsync(8, (u >> 24) & 0xff, cancellationToken).ConfigureAwait(false);
        await BsWAsync(8, (u >> 16) & 0xff, cancellationToken).ConfigureAwait(false);
        await BsWAsync(8, (u >> 8) & 0xff, cancellationToken).ConfigureAwait(false);
        await BsWAsync(8, u & 0xff, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask BsPutIntVSAsync(int numBits, int c, CancellationToken cancellationToken) =>
        BsWAsync(numBits, c, cancellationToken);

    private async ValueTask SendMTFValuesAsync(CancellationToken cancellationToken)
    {
        var len = CBZip2InputStream.InitCharArray(
            BZip2Constants.N_GROUPS,
            BZip2Constants.MAX_ALPHA_SIZE
        );

        int v,
            t,
            i,
            j,
            gs,
            ge,
            totc,
            bt,
            bc,
            iter;
        int nSelectors = 0,
            alphaSize,
            minLen,
            maxLen,
            selCtr;
        int nGroups; //, nBytes;

        alphaSize = nInUse + 2;
        for (t = 0; t < BZip2Constants.N_GROUPS; t++)
        {
            for (v = 0; v < alphaSize; v++)
            {
                len[t][v] = (char)GREATER_ICOST;
            }
        }

        if (nMTF <= 0)
        {
            Panic();
        }

        if (nMTF < 200)
        {
            nGroups = 2;
        }
        else if (nMTF < 600)
        {
            nGroups = 3;
        }
        else if (nMTF < 1200)
        {
            nGroups = 4;
        }
        else if (nMTF < 2400)
        {
            nGroups = 5;
        }
        else
        {
            nGroups = 6;
        }

        {
            int nPart,
                remF,
                tFreq,
                aFreq;

            nPart = nGroups;
            remF = nMTF;
            gs = 0;
            while (nPart > 0)
            {
                tFreq = remF / nPart;
                ge = gs - 1;
                aFreq = 0;
                while (aFreq < tFreq && ge < alphaSize - 1)
                {
                    ge++;
                    aFreq += mtfFreq[ge];
                }

                if (ge > gs && nPart != nGroups && nPart != 1 && ((nGroups - nPart) % 2 == 1))
                {
                    aFreq -= mtfFreq[ge];
                    ge--;
                }

                for (v = 0; v < alphaSize; v++)
                {
                    if (v >= gs && v <= ge)
                    {
                        len[nPart - 1][v] = (char)LESSER_ICOST;
                    }
                    else
                    {
                        len[nPart - 1][v] = (char)GREATER_ICOST;
                    }
                }

                nPart--;
                gs = ge + 1;
                remF -= aFreq;
            }
        }

        var rfreq = CBZip2InputStream.InitIntArray(
            BZip2Constants.N_GROUPS,
            BZip2Constants.MAX_ALPHA_SIZE
        );
        var fave = new int[BZip2Constants.N_GROUPS];
        var cost = new short[BZip2Constants.N_GROUPS];
        for (iter = 0; iter < BZip2Constants.N_ITERS; iter++)
        {
            for (t = 0; t < nGroups; t++)
            {
                fave[t] = 0;
            }

            for (t = 0; t < nGroups; t++)
            {
                for (v = 0; v < alphaSize; v++)
                {
                    rfreq[t][v] = 0;
                }
            }

            nSelectors = 0;
            totc = 0;
            gs = 0;
            while (true)
            {
                if (gs >= nMTF)
                {
                    break;
                }
                ge = gs + BZip2Constants.G_SIZE - 1;
                if (ge >= nMTF)
                {
                    ge = nMTF - 1;
                }

                for (t = 0; t < nGroups; t++)
                {
                    cost[t] = 0;
                }

                if (nGroups == 6)
                {
                    short cost0,
                        cost1,
                        cost2,
                        cost3,
                        cost4,
                        cost5;
                    cost0 = cost1 = cost2 = cost3 = cost4 = cost5 = 0;
                    for (i = gs; i <= ge; i++)
                    {
                        var icv = szptr[i];
                        cost0 += (short)len[0][icv];
                        cost1 += (short)len[1][icv];
                        cost2 += (short)len[2][icv];
                        cost3 += (short)len[3][icv];
                        cost4 += (short)len[4][icv];
                        cost5 += (short)len[5][icv];
                    }
                    cost[0] = cost0;
                    cost[1] = cost1;
                    cost[2] = cost2;
                    cost[3] = cost3;
                    cost[4] = cost4;
                    cost[5] = cost5;
                }
                else
                {
                    for (i = gs; i <= ge; i++)
                    {
                        var icv = szptr[i];
                        for (t = 0; t < nGroups; t++)
                        {
                            cost[t] += (short)len[t][icv];
                        }
                    }
                }

                bc = 999999999;
                bt = -1;
                for (t = 0; t < nGroups; t++)
                {
                    if (cost[t] < bc)
                    {
                        bc = cost[t];
                        bt = t;
                    }
                }
                ;
                totc += bc;
                fave[bt]++;
                selector[nSelectors] = (char)bt;
                nSelectors++;

                for (i = gs; i <= ge; i++)
                {
                    rfreq[bt][szptr[i]]++;
                }

                gs = ge + 1;
            }

            for (t = 0; t < nGroups; t++)
            {
                HbMakeCodeLengths(len[t], rfreq[t], alphaSize, 20);
            }
        }

        rfreq = null;
        fave = null;
        cost = null;

        if (!(nGroups < 8))
        {
            Panic();
        }
        if (!(nSelectors < 32768 && nSelectors <= (2 + (900000 / BZip2Constants.G_SIZE))))
        {
            Panic();
        }

        {
            var pos = new char[BZip2Constants.N_GROUPS];
            char ll_i,
                tmp2,
                tmp;
            for (i = 0; i < nGroups; i++)
            {
                pos[i] = (char)i;
            }
            for (i = 0; i < nSelectors; i++)
            {
                ll_i = selector[i];
                j = 0;
                tmp = pos[j];
                while (ll_i != tmp)
                {
                    j++;
                    tmp2 = tmp;
                    tmp = pos[j];
                    pos[j] = tmp2;
                }
                pos[0] = tmp;
                selectorMtf[i] = (char)j;
            }
        }

        var code = CBZip2InputStream.InitIntArray(
            BZip2Constants.N_GROUPS,
            BZip2Constants.MAX_ALPHA_SIZE
        );

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
            if (maxLen > 20)
            {
                Panic();
            }
            if (minLen < 1)
            {
                Panic();
            }
            HbAssignCodes(code[t], len[t], minLen, maxLen, alphaSize);
        }

        {
            var inUse16 = new bool[16];
            for (i = 0; i < 16; i++)
            {
                inUse16[i] = false;
                for (j = 0; j < 16; j++)
                {
                    if (inUse[(i * 16) + j])
                    {
                        inUse16[i] = true;
                    }
                }
            }

            for (i = 0; i < 16; i++)
            {
                if (inUse16[i])
                {
                    await BsWAsync(1, 1, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await BsWAsync(1, 0, cancellationToken).ConfigureAwait(false);
                }
            }

            for (i = 0; i < 16; i++)
            {
                if (inUse16[i])
                {
                    for (j = 0; j < 16; j++)
                    {
                        if (inUse[(i * 16) + j])
                        {
                            await BsWAsync(1, 1, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            await BsWAsync(1, 0, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        await BsWAsync(3, nGroups, cancellationToken).ConfigureAwait(false);
        await BsWAsync(15, nSelectors, cancellationToken).ConfigureAwait(false);
        for (i = 0; i < nSelectors; i++)
        {
            for (j = 0; j < selectorMtf[i]; j++)
            {
                await BsWAsync(1, 1, cancellationToken).ConfigureAwait(false);
            }
            await BsWAsync(1, 0, cancellationToken).ConfigureAwait(false);
        }

        for (t = 0; t < nGroups; t++)
        {
            int curr = len[t][0];
            await BsWAsync(5, curr, cancellationToken).ConfigureAwait(false);
            for (i = 0; i < alphaSize; i++)
            {
                while (curr < len[t][i])
                {
                    await BsWAsync(2, 2, cancellationToken).ConfigureAwait(false);
                    curr++;
                }
                while (curr > len[t][i])
                {
                    await BsWAsync(2, 3, cancellationToken).ConfigureAwait(false);
                    curr--;
                }
                await BsWAsync(1, 0, cancellationToken).ConfigureAwait(false);
            }
        }

        selCtr = 0;
        gs = 0;
        while (true)
        {
            if (gs >= nMTF)
            {
                break;
            }
            ge = gs + BZip2Constants.G_SIZE - 1;
            if (ge >= nMTF)
            {
                ge = nMTF - 1;
            }
            for (i = gs; i <= ge; i++)
            {
                await BsWAsync(
                        len[selector[selCtr]][szptr[i]],
                        code[selector[selCtr]][szptr[i]],
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }

            gs = ge + 1;
            selCtr++;
        }
        if (!(selCtr == nSelectors))
        {
            Panic();
        }
    }

    private async ValueTask MoveToFrontCodeAndSendAsync(CancellationToken cancellationToken)
    {
        await BsPutIntVSAsync(24, origPtr, cancellationToken).ConfigureAwait(false);
        GenerateMTFValues();
        await SendMTFValuesAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureStreamHeaderWrittenAsync(cancellationToken).ConfigureAwait(false);
        for (var k = 0; k < count; ++k)
        {
            await WriteByteAsync(buffer[k + offset], cancellationToken).ConfigureAwait(false);
        }
    }

#if !LEGACY_DOTNET
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        await EnsureStreamHeaderWrittenAsync(cancellationToken).ConfigureAwait(false);
        for (var k = 0; k < buffer.Length; ++k)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = buffer.Span[k];
            await WriteByteAsync(value, cancellationToken).ConfigureAwait(false);
        }
    }
#endif

    /// <summary>
    /// Asynchronously finalizes the BZip2 compressed stream, flushing all pending data.
    /// Writes the remaining compressed data to the underlying stream using async I/O.
    /// </summary>
    public async ValueTask FinishAsync(CancellationToken cancellationToken = default)
    {
        if (finished)
        {
            return;
        }

        await EnsureStreamHeaderWrittenAsync(cancellationToken).ConfigureAwait(false);

        if (runLength > 0)
        {
            await WriteRunAsync(cancellationToken).ConfigureAwait(false);
        }
        currentChar = -1;
        await EndBlockAsync(cancellationToken).ConfigureAwait(false);
        await EndCompressionAsync(cancellationToken).ConfigureAwait(false);
        finished = true;
        await bsStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

#if !LEGACY_DOTNET || NETSTANDARD2_1
    public override async ValueTask DisposeAsync()
#else
    public async ValueTask DisposeAsync()
#endif
    {
        if (disposed)
        {
            return;
        }

        await FinishAsync().ConfigureAwait(false);
        disposed = true;
        if (!leaveOpen && bsStream is not null)
        {
            if (bsStream is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                bsStream.Dispose();
            }
        }
        bsStream = null;

#if !LEGACY_DOTNET || NETSTANDARD2_1
        await base.DisposeAsync().ConfigureAwait(false);
#else
        await Task.CompletedTask.ConfigureAwait(false);
#endif
        GC.SuppressFinalize(this);
    }
}
