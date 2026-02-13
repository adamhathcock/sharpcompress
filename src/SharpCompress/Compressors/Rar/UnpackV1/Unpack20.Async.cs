using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors.Rar.UnpackV1.Decode;

namespace SharpCompress.Compressors.Rar.UnpackV1;

internal partial class Unpack
{
    private async Task unpack20Async(bool solid, CancellationToken cancellationToken = default)
    {
        int Bits;

        if (suspended)
        {
            unpPtr = wrPtr;
        }
        else
        {
            UnpInitData(solid);
            if (!await unpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return;
            }
            if (!solid)
            {
                if (!await ReadTables20Async(cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            --destUnpSize;
        }

        while (destUnpSize >= 0)
        {
            unpPtr &= PackDef.MAXWINMASK;

            if (inAddr > readTop - 30)
            {
                if (!await unpReadBufAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
            if (((wrPtr - unpPtr) & PackDef.MAXWINMASK) < 270 && wrPtr != unpPtr)
            {
                oldUnpWriteBuf();
                if (suspended)
                {
                    return;
                }
            }
            if (UnpAudioBlock != 0)
            {
                var AudioNumber = this.decodeNumber(MD[UnpCurChannel]);

                if (AudioNumber == 256)
                {
                    if (!await ReadTables20Async(cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }
                    continue;
                }
                window[unpPtr++] = DecodeAudio(AudioNumber);
                if (++UnpCurChannel == UnpChannels)
                {
                    UnpCurChannel = 0;
                }
                --destUnpSize;
                continue;
            }

            var Number = this.decodeNumber(LD);
            if (Number < 256)
            {
                window[unpPtr++] = (byte)Number;
                --destUnpSize;
                continue;
            }
            if (Number > 269)
            {
                var Length = LDecode[Number -= 270] + 3;
                if ((Bits = LBits[Number]) > 0)
                {
                    Length += Utility.URShift(GetBits(), (16 - Bits));
                    AddBits(Bits);
                }

                var DistNumber = this.decodeNumber(DD);
                var Distance = DDecode[DistNumber] + 1;
                if ((Bits = DBits[DistNumber]) > 0)
                {
                    Distance += Utility.URShift(GetBits(), (16 - Bits));
                    AddBits(Bits);
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
                CopyString20(lastLength, lastDist);
                continue;
            }
            if (Number < 261)
            {
                var Distance = oldDist[(oldDistPtr - (Number - 256)) & 3];
                var LengthNumber = this.decodeNumber(RD);
                var Length = LDecode[LengthNumber] + 2;
                if ((Bits = LBits[LengthNumber]) > 0)
                {
                    Length += Utility.URShift(GetBits(), (16 - Bits));
                    AddBits(Bits);
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
                var Distance = SDDecode[Number -= 261] + 1;
                if ((Bits = SDBits[Number]) > 0)
                {
                    Distance += Utility.URShift(GetBits(), (16 - Bits));
                    AddBits(Bits);
                }
                CopyString20(2, Distance);
            }
        }
        ReadLastTables();
        oldUnpWriteBuf();
    }

    private async Task<bool> ReadTables20Async(CancellationToken cancellationToken = default)
    {
        byte[] BitLength = new byte[PackDef.BC20];
        byte[] Table = new byte[PackDef.MC20 * 4];
        int TableSize,
            N,
            I;
        if (inAddr > readTop - 25)
        {
            if (!await unpReadBufAsync(cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }
        var BitField = GetBits();
        UnpAudioBlock = (BitField & 0x8000);

        if (0 == (BitField & 0x4000))
        {
            new Span<byte>(UnpOldTable20).Clear();
        }
        AddBits(2);

        if (UnpAudioBlock != 0)
        {
            UnpChannels = ((Utility.URShift(BitField, 12)) & 3) + 1;
            if (UnpCurChannel >= UnpChannels)
            {
                UnpCurChannel = 0;
            }
            AddBits(2);
            TableSize = PackDef.MC20 * UnpChannels;
        }
        else
        {
            TableSize = PackDef.NC20 + PackDef.DC20 + PackDef.RC20;
        }
        for (I = 0; I < PackDef.BC20; I++)
        {
            BitLength[I] = (byte)(Utility.URShift(GetBits(), 12));
            AddBits(4);
        }
        UnpackUtility.makeDecodeTables(BitLength, 0, BD, PackDef.BC20);
        I = 0;
        while (I < TableSize)
        {
            if (inAddr > readTop - 5)
            {
                if (!await unpReadBufAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }
            }
            var Number = this.decodeNumber(BD);
            if (Number < 16)
            {
                Table[I] = (byte)((Number + UnpOldTable20[I]) & 0xf);
                I++;
            }
            else if (Number == 16)
            {
                N = (Utility.URShift(GetBits(), 14)) + 3;
                AddBits(2);
                while (N-- > 0 && I < TableSize)
                {
                    Table[I] = Table[I - 1];
                    I++;
                }
            }
            else
            {
                if (Number == 17)
                {
                    N = (Utility.URShift(GetBits(), 13)) + 3;
                    AddBits(3);
                }
                else
                {
                    N = (Utility.URShift(GetBits(), 9)) + 11;
                    AddBits(7);
                }
                while (N-- > 0 && I < TableSize)
                {
                    Table[I++] = 0;
                }
            }
        }
        if (inAddr > readTop)
        {
            return true;
        }
        if (UnpAudioBlock != 0)
        {
            for (I = 0; I < UnpChannels; I++)
            {
                UnpackUtility.makeDecodeTables(Table, I * PackDef.MC20, MD[I], PackDef.MC20);
            }
        }
        else
        {
            UnpackUtility.makeDecodeTables(Table, 0, LD, PackDef.NC20);
            UnpackUtility.makeDecodeTables(Table, PackDef.NC20, DD, PackDef.DC20);
            UnpackUtility.makeDecodeTables(Table, PackDef.NC20 + PackDef.DC20, RD, PackDef.RC20);
        }

        for (var i = 0; i < UnpOldTable20.Length; i++)
        {
            UnpOldTable20[i] = Table[i];
        }
        return true;
    }
}
