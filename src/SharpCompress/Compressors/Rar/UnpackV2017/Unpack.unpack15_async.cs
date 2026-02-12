using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Compressors.Rar.UnpackV2017;

internal partial class Unpack
{
    private async Task Unpack15Async(bool Solid, CancellationToken cancellationToken = default)
    {
        UnpInitData(Solid);
        UnpInitData15(Solid);
        await UnpReadBufAsync(cancellationToken).ConfigureAwait(false);
        if (!Solid)
        {
            InitHuff();
            UnpPtr = 0;
        }
        else
        {
            UnpPtr = WrPtr;
        }

        --DestUnpSize;
        if (DestUnpSize >= 0)
        {
            GetFlagsBuf();
            FlagsCnt = 8;
        }

        while (DestUnpSize >= 0)
        {
            UnpPtr &= MaxWinMask;

            if (
                Inp.InAddr > ReadTop - 30
                && !await UnpReadBufAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                break;
            }

            if (((WrPtr - UnpPtr) & MaxWinMask) < 270 && WrPtr != UnpPtr)
            {
                UnpWriteBuf20();
            }

            if (StMode != 0)
            {
                HuffDecode();
                continue;
            }

            if (--FlagsCnt < 0)
            {
                GetFlagsBuf();
                FlagsCnt = 7;
            }

            if ((FlagBuf & 0x80) != 0)
            {
                FlagBuf <<= 1;
                if (Nlzb > Nhfb)
                {
                    LongLZ();
                }
                else
                {
                    HuffDecode();
                }
            }
            else
            {
                FlagBuf <<= 1;
                if (--FlagsCnt < 0)
                {
                    GetFlagsBuf();
                    FlagsCnt = 7;
                }
                if ((FlagBuf & 0x80) != 0)
                {
                    FlagBuf <<= 1;
                    if (Nlzb > Nhfb)
                    {
                        HuffDecode();
                    }
                    else
                    {
                        LongLZ();
                    }
                }
                else
                {
                    FlagBuf <<= 1;
                    ShortLZ();
                }
            }
        }
        UnpWriteBuf20();
    }
}
