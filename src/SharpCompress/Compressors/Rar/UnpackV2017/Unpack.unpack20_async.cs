using System;
using System.Threading;
using System.Threading.Tasks;
using static SharpCompress.Compressors.Rar.UnpackV2017.PackDef;
using static SharpCompress.Compressors.Rar.UnpackV2017.Unpack.Unpack20Local;

namespace SharpCompress.Compressors.Rar.UnpackV2017;

internal partial class Unpack
{
    private async Task Unpack20Async(bool Solid, CancellationToken cancellationToken = default)
    {
        uint Bits;

        if (Suspended)
        {
            UnpPtr = WrPtr;
        }
        else
        {
            UnpInitData(Solid);
            if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (
                (!Solid || !TablesRead2)
                && !await ReadTables20Async(cancellationToken).ConfigureAwait(false)
            )
            {
                return;
            }

            --DestUnpSize;
        }

        while (DestUnpSize >= 0)
        {
            UnpPtr &= MaxWinMask;

            if (Inp.InAddr > ReadTop - 30)
            {
                if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }

            if (((WrPtr - UnpPtr) & MaxWinMask) < 270 && WrPtr != UnpPtr)
            {
                UnpWriteBuf20();
                if (Suspended)
                {
                    return;
                }
            }
            if (UnpAudioBlock)
            {
                var AudioNumber = DecodeNumber(Inp, MD[UnpCurChannel]);

                if (AudioNumber == 256)
                {
                    if (!await ReadTables20Async(cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }

                    continue;
                }
                Window[UnpPtr++] = DecodeAudio((int)AudioNumber);
                if (++UnpCurChannel == UnpChannels)
                {
                    UnpCurChannel = 0;
                }

                --DestUnpSize;
                continue;
            }

            var Number = DecodeNumber(Inp, BlockTables.LD);
            if (Number < 256)
            {
                Window[UnpPtr++] = (byte)Number;
                --DestUnpSize;
                continue;
            }
            if (Number > 269)
            {
                var Length = (uint)(LDecode[Number -= 270] + 3);
                if ((Bits = LBits[Number]) > 0)
                {
                    Length += Inp.getbits() >> (int)(16 - Bits);
                    Inp.addbits(Bits);
                }

                var DistNumber = DecodeNumber(Inp, BlockTables.DD);
                var Distance = DDecode[DistNumber] + 1;
                if ((Bits = DBits[DistNumber]) > 0)
                {
                    Distance += Inp.getbits() >> (int)(16 - Bits);
                    Inp.addbits(Bits);
                }

                if (Distance >= 0x2000)
                {
                    Length++;
                    if (Distance >= 0x40000L)
                    {
                        Length++;
                    }
                }

                CopyString20(Length, Distance);
                continue;
            }
            if (Number == 269)
            {
                if (!await ReadTables20Async(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }

                continue;
            }
            if (Number == 256)
            {
                CopyString20(LastLength, LastDist);
                continue;
            }
            if (Number < 261)
            {
                var Distance = OldDist[(OldDistPtr - (Number - 256)) & 3];
                var LengthNumber = DecodeNumber(Inp, BlockTables.RD);
                var Length = (uint)(LDecode[LengthNumber] + 2);
                if ((Bits = LBits[LengthNumber]) > 0)
                {
                    Length += Inp.getbits() >> (int)(16 - Bits);
                    Inp.addbits(Bits);
                }
                if (Distance >= 0x101)
                {
                    Length++;
                    if (Distance >= 0x2000)
                    {
                        Length++;
                        if (Distance >= 0x40000)
                        {
                            Length++;
                        }
                    }
                }
                CopyString20(Length, Distance);
                continue;
            }
            if (Number < 270)
            {
                var Distance = (uint)(SDDecode[Number -= 261] + 1);
                if ((Bits = SDBits[Number]) > 0)
                {
                    Distance += Inp.getbits() >> (int)(16 - Bits);
                    Inp.addbits(Bits);
                }
                CopyString20(2, Distance);
                continue;
            }
        }
        ReadLastTables();
        UnpWriteBuf20();
    }

    private async Task UnpWriteBuf20Async(CancellationToken cancellationToken = default)
    {
        if (UnpPtr != WrPtr)
        {
            UnpSomeRead = true;
        }

        if (UnpPtr < WrPtr)
        {
            await UnpIO_UnpWriteAsync(
                    Window,
                    WrPtr,
                    (uint)(-(int)WrPtr & MaxWinMask),
                    cancellationToken
                )
                .ConfigureAwait(false);
            await UnpIO_UnpWriteAsync(Window, 0, UnpPtr, cancellationToken).ConfigureAwait(false);
            UnpAllBuf = true;
        }
        else
        {
            await UnpIO_UnpWriteAsync(Window, WrPtr, UnpPtr - WrPtr, cancellationToken)
                .ConfigureAwait(false);
        }

        WrPtr = UnpPtr;
    }

    private async Task<bool> ReadTables20Async(CancellationToken cancellationToken = default)
    {
        byte[] BitLength = new byte[checked((int)BC20)];
        byte[] Table = new byte[checked((int)MC20 * 4)];
        if (Inp.InAddr > ReadTop - 25)
        {
            if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        var BitField = Inp.getbits();
        UnpAudioBlock = (BitField & 0x8000) != 0;

        if ((BitField & 0x4000) != 0)
        {
            Array.Clear(UnpOldTable20, 0, UnpOldTable20.Length);
        }

        Inp.addbits(2);

        uint TableSize;
        if (UnpAudioBlock)
        {
            UnpChannels = ((BitField >> 12) & 3) + 1;
            if (UnpCurChannel >= UnpChannels)
            {
                UnpCurChannel = 0;
            }

            Inp.addbits(2);
            TableSize = MC20 * UnpChannels;
        }
        else
        {
            TableSize = NC20 + DC20 + RC20;
        }

        for (int I = 0; I < checked((int)BC20); I++)
        {
            BitLength[I] = (byte)(Inp.getbits() >> 12);
            Inp.addbits(4);
        }
        MakeDecodeTables(BitLength, 0, BlockTables.BD, BC20);
        for (int I = 0; I < checked((int)TableSize); )
        {
            if (Inp.InAddr > ReadTop - 5)
            {
                if (!await UnpReadBufAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }

            var Number = DecodeNumber(Inp, BlockTables.BD);
            if (Number < 16)
            {
                Table[I] = (byte)((Number + UnpOldTable20[I]) & 0xf);
                I++;
            }
            else if (Number == 16)
            {
                var N = (Inp.getbits() >> 14) + 3;
                Inp.addbits(2);
                if (I == 0)
                {
                    return false; // We cannot have "repeat previous" code at the first position.
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
                if (Number == 17)
                {
                    N = (Inp.getbits() >> 13) + 3;
                    Inp.addbits(3);
                }
                else
                {
                    N = (Inp.getbits() >> 9) + 11;
                    Inp.addbits(7);
                }
                while (N-- > 0 && I < TableSize)
                {
                    Table[I++] = 0;
                }
            }
        }
        TablesRead2 = true;
        if (Inp.InAddr > ReadTop)
        {
            return true;
        }

        if (UnpAudioBlock)
        {
            for (int I = 0; I < UnpChannels; I++)
            {
                MakeDecodeTables(Table, (int)(I * MC20), MD[I], MC20);
            }
        }
        else
        {
            MakeDecodeTables(Table, 0, BlockTables.LD, NC20);
            MakeDecodeTables(Table, (int)NC20, BlockTables.DD, DC20);
            MakeDecodeTables(Table, (int)(NC20 + DC20), BlockTables.RD, RC20);
        }
        Array.Copy(Table, 0, this.UnpOldTable20, 0, UnpOldTable20.Length);
        return true;
    }
}
