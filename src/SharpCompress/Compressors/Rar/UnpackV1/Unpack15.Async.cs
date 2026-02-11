using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Rar.UnpackV1.Decode;

namespace SharpCompress.Compressors.Rar.UnpackV1;

internal partial class Unpack
{
    private async Task unpack15Async(bool solid, CancellationToken cancellationToken = default)
    {
        if (suspended)
        {
            unpPtr = wrPtr;
        }
        else
        {
            UnpInitData(solid);
            oldUnpInitData(solid);
            await unpReadBufAsync(cancellationToken).ConfigureAwait(false);
            if (!solid)
            {
                initHuff();
                unpPtr = 0;
            }
            else
            {
                unpPtr = wrPtr;
            }
            --destUnpSize;
        }
        if (destUnpSize >= 0)
        {
            getFlagsBuf();
            FlagsCnt = 8;
        }

        while (destUnpSize >= 0)
        {
            unpPtr &= PackDef.MAXWINMASK;

            if (
                inAddr > readTop - 30
                && !await unpReadBufAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                break;
            }
            if (((wrPtr - unpPtr) & PackDef.MAXWINMASK) < 270 && wrPtr != unpPtr)
            {
                oldUnpWriteBuf();
                if (suspended)
                {
                    return;
                }
            }
            if (StMode != 0)
            {
                huffDecode();
                continue;
            }

            if (--FlagsCnt < 0)
            {
                getFlagsBuf();
                FlagsCnt = 7;
            }

            if ((FlagBuf & 0x80) != 0)
            {
                FlagBuf <<= 1;
                if (Nlzb > Nhfb)
                {
                    longLZ();
                }
                else
                {
                    huffDecode();
                }
            }
            else
            {
                FlagBuf <<= 1;
                if (--FlagsCnt < 0)
                {
                    getFlagsBuf();
                    FlagsCnt = 7;
                }
                if ((FlagBuf & 0x80) != 0)
                {
                    FlagBuf <<= 1;
                    if (Nlzb > Nhfb)
                    {
                        huffDecode();
                    }
                    else
                    {
                        longLZ();
                    }
                }
                else
                {
                    FlagBuf <<= 1;
                    shortLZ();
                }
            }
        }
        oldUnpWriteBuf();
    }

    private async Task<bool> unpReadBufAsync(CancellationToken cancellationToken = default)
    {
        var dataSize = readTop - inAddr;
        if (dataSize < 0)
        {
            return false;
        }
        if (inAddr > MAX_SIZE / 2)
        {
            if (dataSize > 0)
            {
                Array.Copy(InBuf, inAddr, InBuf, 0, dataSize);
            }
            inAddr = 0;
            readTop = dataSize;
        }
        else
        {
            dataSize = readTop;
        }

        var readCode = await readStream
            .ReadAsync(InBuf, dataSize, (MAX_SIZE - dataSize) & ~0xf, cancellationToken)
            .ConfigureAwait(false);
        if (readCode > 0)
        {
            readTop += readCode;
        }
        readBorder = readTop - 30;
        return readCode != -1;
    }

    private async Task oldUnpWriteBufAsync(CancellationToken cancellationToken = default)
    {
        if (unpPtr < wrPtr)
        {
            await writeStream
                .WriteAsync(window, wrPtr, -wrPtr & PackDef.MAXWINMASK, cancellationToken)
                .ConfigureAwait(false);
            await writeStream
                .WriteAsync(window, 0, unpPtr, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await writeStream
                .WriteAsync(window, wrPtr, unpPtr - wrPtr, cancellationToken)
                .ConfigureAwait(false);
        }
        wrPtr = unpPtr;
    }
}
