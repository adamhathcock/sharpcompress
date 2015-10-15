namespace SharpCompress.Compressor.Rar.VM
{
    using SharpCompress;
    using SharpCompress.Compressor.Rar;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    //internal class RarVM : BitInput
    //{
    //    [CompilerGenerated]
    //    private byte[] _Mem_k__BackingField;
    //    private int codeSize;
    //    private VMFlags flags;
    //    private int IP;
    //    private int maxOpCount = 0x17d7840;
    //    private int[] R = new int[8];
    //    private const int regCount = 8;
    //    private const long UINT_MASK = 0xffffffffL;
    //    public const int VM_FIXEDGLOBALSIZE = 0x40;
    //    public const int VM_GLOBALMEMADDR = 0x3c000;
    //    public const int VM_GLOBALMEMSIZE = 0x2000;
    //    public static readonly int VM_MEMMASK = 0x3ffff;
    //    public const int VM_MEMSIZE = 0x40000;

    //    internal RarVM()
    //    {
    //        this.Mem = null;
    //    }

    //    private void decodeArg(VMPreparedOperand op, bool byteMode)
    //    {
    //        int bits = base.GetBits();
    //        if ((bits & 0x8000) != 0)
    //        {
    //            op.Type = VMOpType.VM_OPREG;
    //            op.Data = (bits >> 12) & 7;
    //            op.Offset = op.Data;
    //            base.AddBits(4);
    //        }
    //        else if ((bits & 0xc000) == 0)
    //        {
    //            op.Type = VMOpType.VM_OPINT;
    //            if (byteMode)
    //            {
    //                op.Data = (bits >> 6) & 0xff;
    //                base.AddBits(10);
    //            }
    //            else
    //            {
    //                base.AddBits(2);
    //                op.Data = ReadData(this);
    //            }
    //        }
    //        else
    //        {
    //            op.Type = VMOpType.VM_OPREGMEM;
    //            if ((bits & 0x2000) == 0)
    //            {
    //                op.Data = (bits >> 10) & 7;
    //                op.Offset = op.Data;
    //                op.Base = 0;
    //                base.AddBits(6);
    //            }
    //            else
    //            {
    //                if ((bits & 0x1000) == 0)
    //                {
    //                    op.Data = (bits >> 9) & 7;
    //                    op.Offset = op.Data;
    //                    base.AddBits(7);
    //                }
    //                else
    //                {
    //                    op.Data = 0;
    //                    base.AddBits(4);
    //                }
    //                op.Base = ReadData(this);
    //            }
    //        }
    //    }

    //    public void execute(VMPreparedProgram prg)
    //    {
    //        int num;
    //        for (num = 0; num < prg.InitR.Length; num++)
    //        {
    //            this.R[num] = prg.InitR[num];
    //        }
    //        long num2 = Math.Min(prg.GlobalData.Count, 0x2000) & ((long) 0xffffffffL);
    //        if (num2 != 0L)
    //        {
    //            for (num = 0; num < num2; num++)
    //            {
    //                this.Mem[0x3c000 + num] = prg.GlobalData[num];
    //            }
    //        }
    //        long num3 = Math.Min((long) prg.StaticData.Count, 0x2000L - num2) & ((long) 0xffffffffL);
    //        if (num3 != 0L)
    //        {
    //            for (num = 0; num < num3; num++)
    //            {
    //                this.Mem[(0x3c000 + ((int) num2)) + num] = prg.StaticData[num];
    //            }
    //        }
    //        this.R[7] = 0x40000;
    //        this.flags = VMFlags.None;
    //        List<VMPreparedCommand> preparedCode = (prg.AltCommands.Count != 0) ? prg.AltCommands : prg.Commands;
    //        if (!this.ExecuteCode(preparedCode, prg.CommandCount))
    //        {
    //            preparedCode[0].OpCode = VMCommands.VM_RET;
    //        }
    //        int num4 = this.GetValue(false, this.Mem, 0x3c020) & VM_MEMMASK;
    //        int num5 = this.GetValue(false, this.Mem, 0x3c01c) & VM_MEMMASK;
    //        if ((num4 + num5) >= 0x40000)
    //        {
    //            num4 = 0;
    //            num5 = 0;
    //        }
    //        prg.FilteredDataOffset = num4;
    //        prg.FilteredDataSize = num5;
    //        prg.GlobalData.Clear();
    //        int num6 = Math.Min(this.GetValue(false, this.Mem, 0x3c030), 0x1fc0);
    //        if (num6 != 0)
    //        {
    //            Utility.SetSize(prg.GlobalData, num6 + 0x40);
    //            for (num = 0; num < (num6 + 0x40); num++)
    //            {
    //                prg.GlobalData[num] = this.Mem[0x3c000 + num];
    //            }
    //        }
    //    }

    //    private bool ExecuteCode(List<VMPreparedCommand> preparedCode, int cmdCount)
    //    {
    //        VMFlags flags;
    //        VMFlags flags2;
    //        int num3;
    //        int num4;
    //        int num5;
    //        int num6;
    //        int num7;
    //        int num9;
    //        bool flag2;
    //        this.maxOpCount = 0x17d7840;
    //        this.codeSize = cmdCount;
    //        this.IP = 0;
    //    Label_10B0:
    //        flag2 = true;
    //        VMPreparedCommand command = preparedCode[this.IP];
    //        int operand = this.GetOperand(command.Op1);
    //        int offset = this.GetOperand(command.Op2);
    //        switch (command.OpCode)
    //        {
    //            case VMCommands.VM_MOV:
    //                this.SetValue(command.IsByteMode, this.Mem, operand, this.GetValue(command.IsByteMode, this.Mem, offset));
    //                break;

    //            case VMCommands.VM_CMP:
    //                flags = (VMFlags) this.GetValue(command.IsByteMode, this.Mem, operand);
    //                flags2 = flags - this.GetValue(command.IsByteMode, this.Mem, offset);
    //                if (flags2 != VMFlags.None)
    //                {
    //                    this.flags = (flags2 > flags) ? VMFlags.VM_FC : (flags2 & VMFlags.VM_FS);
    //                    break;
    //                }
    //                this.flags = VMFlags.VM_FZ;
    //                break;

    //            case VMCommands.VM_ADD:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                num4 = (num3 + this.GetValue(command.IsByteMode, this.Mem, offset)) & ((int) (-1L));
    //                if (!command.IsByteMode)
    //                {
    //                    this.flags = (num4 < num3) ? VMFlags.VM_FC : ((num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS));
    //                }
    //                else
    //                {
    //                    num4 &= 0xff;
    //                    this.flags = (num4 < num3) ? VMFlags.VM_FC : ((num4 == 0) ? VMFlags.VM_FZ : (((num4 & 0x80) != 0) ? VMFlags.VM_FS : VMFlags.None));
    //                }
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_SUB:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                num4 = (int) ((num3 & (0xffffffffL - this.GetValue(command.IsByteMode, this.Mem, offset))) & ulong.MaxValue);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : ((num4 > num3) ? VMFlags.VM_FC : (((VMFlags) num4) & VMFlags.VM_FS));
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_JZ:
    //                if ((this.flags & VMFlags.VM_FZ) == VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_JNZ:
    //                if ((this.flags & VMFlags.VM_FZ) != VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_INC:
    //                num4 = this.GetValue(command.IsByteMode, this.Mem, operand) & ((int) 0x100000000L);
    //                if (command.IsByteMode)
    //                {
    //                    num4 &= 0xff;
    //                }
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS);
    //                break;

    //            case VMCommands.VM_DEC:
    //                num4 = this.GetValue(command.IsByteMode, this.Mem, operand) & ((int) 0xfffffffeL);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS);
    //                break;

    //            case VMCommands.VM_JMP:
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_XOR:
    //                num4 = this.GetValue(command.IsByteMode, this.Mem, operand) ^ this.GetValue(command.IsByteMode, this.Mem, offset);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_AND:
    //                num4 = this.GetValue(command.IsByteMode, this.Mem, operand) & this.GetValue(command.IsByteMode, this.Mem, offset);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_OR:
    //                num4 = this.GetValue(command.IsByteMode, this.Mem, operand) | this.GetValue(command.IsByteMode, this.Mem, offset);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_TEST:
    //                num4 = this.GetValue(command.IsByteMode, this.Mem, operand) & this.GetValue(command.IsByteMode, this.Mem, offset);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS);
    //                break;

    //            case VMCommands.VM_JS:
    //                if ((this.flags & VMFlags.VM_FS) == VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_JNS:
    //                if ((this.flags & VMFlags.VM_FS) != VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_JB:
    //                if ((this.flags & VMFlags.VM_FC) == VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_JBE:
    //                if ((this.flags & (VMFlags.VM_FZ | VMFlags.VM_FC)) == VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_JA:
    //                if ((this.flags & (VMFlags.VM_FZ | VMFlags.VM_FC)) != VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_JAE:
    //                if ((this.flags & VMFlags.VM_FC) != VMFlags.None)
    //                {
    //                    break;
    //                }
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_PUSH:
    //                this.R[7] -= 4;
    //                this.SetValue(false, this.Mem, this.R[7] & VM_MEMMASK, this.GetValue(false, this.Mem, operand));
    //                break;

    //            case VMCommands.VM_POP:
    //                this.SetValue(false, this.Mem, operand, this.GetValue(false, this.Mem, this.R[7] & VM_MEMMASK));
    //                this.R[7] += 4;
    //                break;

    //            case VMCommands.VM_CALL:
    //                this.R[7] -= 4;
    //                this.SetValue(false, this.Mem, this.R[7] & VM_MEMMASK, this.IP + 1);
    //                this.setIP(this.GetValue(false, this.Mem, operand));
    //                goto Label_10B0;

    //            case VMCommands.VM_RET:
    //                if (this.R[7] < 0x40000)
    //                {
    //                    this.setIP(this.GetValue(false, this.Mem, this.R[7] & VM_MEMMASK));
    //                    this.R[7] += 4;
    //                    goto Label_10B0;
    //                }
    //                return true;

    //            case VMCommands.VM_NOT:
    //                this.SetValue(command.IsByteMode, this.Mem, operand, ~this.GetValue(command.IsByteMode, this.Mem, operand));
    //                break;

    //            case VMCommands.VM_SHL:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                num5 = this.GetValue(command.IsByteMode, this.Mem, offset);
    //                num4 = num3 << num5;
    //                this.flags = ((num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS)) | ((((num3 << (num5 - 1)) & -2147483648) != 0) ? VMFlags.VM_FC : VMFlags.None);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_SHR:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                num5 = this.GetValue(command.IsByteMode, this.Mem, offset);
    //                num4 = Utility.URShift(num3, num5);
    //                this.flags = ((num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS)) | (((VMFlags) Utility.URShift(num3, (int) (num5 - 1))) & VMFlags.VM_FC);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_SAR:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                num5 = this.GetValue(command.IsByteMode, this.Mem, offset);
    //                num4 = num3 >> num5;
    //                this.flags = ((num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS)) | (((VMFlags) (num3 >> (num5 - 1))) & VMFlags.VM_FC);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_NEG:
    //                num4 = -this.GetValue(command.IsByteMode, this.Mem, operand);
    //                this.flags = (num4 == 0) ? VMFlags.VM_FZ : (VMFlags.VM_FC | (((VMFlags) num4) & VMFlags.VM_FS));
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_PUSHA:
    //                num6 = 0;
    //                for (num7 = this.R[7] - 4; num6 < 8; num7 -= 4)
    //                {
    //                    this.SetValue(false, this.Mem, num7 & VM_MEMMASK, this.R[num6]);
    //                    num6++;
    //                }
    //                this.R[7] -= 0x20;
    //                break;

    //            case VMCommands.VM_POPA:
    //                num6 = 0;
    //                for (num7 = this.R[7]; num6 < 8; num7 += 4)
    //                {
    //                    this.R[7 - num6] = this.GetValue(false, this.Mem, num7 & VM_MEMMASK);
    //                    num6++;
    //                }
    //                break;

    //            case VMCommands.VM_PUSHF:
    //                this.R[7] -= 4;
    //                this.SetValue(false, this.Mem, this.R[7] & VM_MEMMASK, (int) this.flags);
    //                break;

    //            case VMCommands.VM_POPF:
    //                this.flags = (VMFlags) this.GetValue(false, this.Mem, this.R[7] & VM_MEMMASK);
    //                this.R[7] += 4;
    //                break;

    //            case VMCommands.VM_MOVZX:
    //                this.SetValue(false, this.Mem, operand, this.GetValue(true, this.Mem, offset));
    //                break;

    //            case VMCommands.VM_MOVSX:
    //                this.SetValue(false, this.Mem, operand, (byte) this.GetValue(true, this.Mem, offset));
    //                break;

    //            case VMCommands.VM_XCHG:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, this.GetValue(command.IsByteMode, this.Mem, offset));
    //                this.SetValue(command.IsByteMode, this.Mem, offset, num3);
    //                break;

    //            case VMCommands.VM_MUL:
    //                num4 = (int) (((this.GetValue(command.IsByteMode, this.Mem, operand) & (0xffffffffL * this.GetValue(command.IsByteMode, this.Mem, offset))) & ulong.MaxValue) & ulong.MaxValue);
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_DIV:
    //            {
    //                int num8 = this.GetValue(command.IsByteMode, this.Mem, offset);
    //                if (num8 != 0)
    //                {
    //                    num4 = this.GetValue(command.IsByteMode, this.Mem, operand) / num8;
    //                    this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                }
    //                break;
    //            }
    //            case VMCommands.VM_ADC:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                num9 = ((int) this.flags) & 1;
    //                num4 = (int) (((num3 & (0xffffffffL + this.GetValue(command.IsByteMode, this.Mem, offset))) & (0xffffffffL + num9)) & ulong.MaxValue);
    //                if (command.IsByteMode)
    //                {
    //                    num4 &= 0xff;
    //                }
    //                this.flags = ((num4 < num3) || ((num4 == num3) && (num9 != 0))) ? VMFlags.VM_FC : ((num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS));
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_SBB:
    //                num3 = this.GetValue(command.IsByteMode, this.Mem, operand);
    //                num9 = ((int) this.flags) & 1;
    //                num4 = (int) (((num3 & (0xffffffffL - this.GetValue(command.IsByteMode, this.Mem, offset))) & (0xffffffffL - num9)) & ulong.MaxValue);
    //                if (command.IsByteMode)
    //                {
    //                    num4 &= 0xff;
    //                }
    //                this.flags = ((num4 > num3) || ((num4 == num3) && (num9 != 0))) ? VMFlags.VM_FC : ((num4 == 0) ? VMFlags.VM_FZ : (((VMFlags) num4) & VMFlags.VM_FS));
    //                this.SetValue(command.IsByteMode, this.Mem, operand, num4);
    //                break;

    //            case VMCommands.VM_MOVB:
    //                this.SetValue(true, this.Mem, operand, this.GetValue(true, this.Mem, offset));
    //                break;

    //            case VMCommands.VM_MOVD:
    //                this.SetValue(false, this.Mem, operand, this.GetValue(false, this.Mem, offset));
    //                break;

    //            case VMCommands.VM_CMPB:
    //                flags = (VMFlags) this.GetValue(true, this.Mem, operand);
    //                flags2 = flags - this.GetValue(true, this.Mem, offset);
    //                if (flags2 != VMFlags.None)
    //                {
    //                    this.flags = (flags2 > flags) ? VMFlags.VM_FC : (flags2 & VMFlags.VM_FS);
    //                    break;
    //                }
    //                this.flags = VMFlags.VM_FZ;
    //                break;

    //            case VMCommands.VM_CMPD:
    //                flags = (VMFlags) this.GetValue(false, this.Mem, operand);
    //                flags2 = flags - this.GetValue(false, this.Mem, offset);
    //                if (flags2 != VMFlags.None)
    //                {
    //                    this.flags = (flags2 > flags) ? VMFlags.VM_FC : (flags2 & VMFlags.VM_FS);
    //                    break;
    //                }
    //                this.flags = VMFlags.VM_FZ;
    //                break;

    //            case VMCommands.VM_ADDB:
    //                this.SetValue(true, this.Mem, operand, (int) ((this.GetValue(true, this.Mem, operand) & (0xffffffffL + this.GetValue(true, this.Mem, offset))) & ulong.MaxValue));
    //                break;

    //            case VMCommands.VM_ADDD:
    //                this.SetValue(false, this.Mem, operand, (int) ((this.GetValue(false, this.Mem, operand) & (0xffffffffL + this.GetValue(false, this.Mem, offset))) & ulong.MaxValue));
    //                break;

    //            case VMCommands.VM_SUBB:
    //                this.SetValue(true, this.Mem, operand, (int) ((this.GetValue(true, this.Mem, operand) & (0xffffffffL - this.GetValue(true, this.Mem, offset))) & ulong.MaxValue));
    //                break;

    //            case VMCommands.VM_SUBD:
    //                this.SetValue(false, this.Mem, operand, (int) ((this.GetValue(false, this.Mem, operand) & (0xffffffffL - this.GetValue(false, this.Mem, offset))) & ulong.MaxValue));
    //                break;

    //            case VMCommands.VM_INCB:
    //                this.SetValue(true, this.Mem, operand, this.GetValue(true, this.Mem, operand) & ((int) 0x100000000L));
    //                break;

    //            case VMCommands.VM_INCD:
    //                this.SetValue(false, this.Mem, operand, this.GetValue(false, this.Mem, operand) & ((int) 0x100000000L));
    //                break;

    //            case VMCommands.VM_DECB:
    //                this.SetValue(true, this.Mem, operand, this.GetValue(true, this.Mem, operand) & ((int) 0xfffffffeL));
    //                break;

    //            case VMCommands.VM_DECD:
    //                this.SetValue(false, this.Mem, operand, this.GetValue(false, this.Mem, operand) & ((int) 0xfffffffeL));
    //                break;

    //            case VMCommands.VM_NEGB:
    //                this.SetValue(true, this.Mem, operand, -this.GetValue(true, this.Mem, operand));
    //                break;

    //            case VMCommands.VM_NEGD:
    //                this.SetValue(false, this.Mem, operand, -this.GetValue(false, this.Mem, operand));
    //                break;

    //            case VMCommands.VM_STANDARD:
    //                this.ExecuteStandardFilter((VMStandardFilters) command.Op1.Data);
    //                break;
    //        }
    //        this.IP++;
    //        this.maxOpCount--;
    //        goto Label_10B0;
    //    }

    //    private void ExecuteStandardFilter(VMStandardFilters filterType)
    //    {
    //        int num;
    //        long num2;
    //        int num5;
    //        byte num6;
    //        int num11;
    //        int num15;
    //        int num16;
    //        int num17;
    //        int num18;
    //        int num20;
    //        int num23;
    //        long num24;
    //        long num25;
    //        switch (filterType)
    //        {
    //            case VMStandardFilters.VMSF_E8:
    //            case VMStandardFilters.VMSF_E8E9:
    //                num = this.R[4];
    //                num2 = this.R[6] & -1;
    //                if (num < 0x3c000)
    //                {
    //                    int num3 = 0x1000000;
    //                    byte num4 = (filterType == VMStandardFilters.VMSF_E8E9) ? ((byte) 0xe9) : ((byte) 0xe8);
    //                    num5 = 0;
    //                    while (num5 < (num - 4))
    //                    {
    //                        num6 = this.Mem[num5++];
    //                        if ((num6 == 0xe8) || (num6 == num4))
    //                        {
    //                            long num7 = num5 + num2;
    //                            long num8 = this.GetValue(false, this.Mem, num5);
    //                            if ((num8 & -2147483648L) != 0L)
    //                            {
    //                                if (((num8 + num7) & -2147483648L) == 0L)
    //                                {
    //                                    this.SetValue(false, this.Mem, num5, ((int) num8) + num3);
    //                                }
    //                            }
    //                            else if (((num8 - num3) & -2147483648L) != 0L)
    //                            {
    //                                this.SetValue(false, this.Mem, num5, (int) (num8 - num7));
    //                            }
    //                            num5 += 4;
    //                        }
    //                    }
    //                    break;
    //                }
    //                break;

    //            case VMStandardFilters.VMSF_ITANIUM:
    //                num = this.R[4];
    //                num2 = this.R[6] & -1;
    //                if (num < 0x3c000)
    //                {
    //                    num5 = 0;
    //                    byte[] buffer = new byte[] { 4, 4, 6, 6, 0, 0, 7, 7, 4, 4, 0, 0, 4, 4, 0, 0 };
    //                    for (num2 = Utility.URShift(num2, 4); num5 < (num - 0x15); num2 += 1L)
    //                    {
    //                        int index = (this.Mem[num5] & 0x1f) - 0x10;
    //                        if (index >= 0)
    //                        {
    //                            byte num10 = buffer[index];
    //                            if (num10 != 0)
    //                            {
    //                                num11 = 0;
    //                                while (num11 <= 2)
    //                                {
    //                                    if ((num10 & (((int) 1) << num11)) != 0)
    //                                    {
    //                                        int num12 = (num11 * 0x29) + 5;
    //                                        if (this.filterItanium_GetBits(num5, num12 + 0x25, 4) == 5)
    //                                        {
    //                                            int num14 = this.filterItanium_GetBits(num5, num12 + 13, 20);
    //                                            this.filterItanium_SetBits(num5, (num14 - ((int) num2)) & 0xfffff, num12 + 13, 20);
    //                                        }
    //                                    }
    //                                    num11++;
    //                                }
    //                            }
    //                        }
    //                        num5 += 0x10;
    //                    }
    //                    break;
    //                }
    //                break;

    //            case VMStandardFilters.VMSF_RGB:
    //            {
    //                num = this.R[4];
    //                int num21 = this.R[0] - 3;
    //                int num22 = this.R[1];
    //                num15 = 3;
    //                num16 = 0;
    //                num23 = num;
    //                this.SetValue(false, this.Mem, 0x3c020, num);
    //                if ((num < 0x1e000) && (num22 >= 0))
    //                {
    //                    num18 = 0;
    //                    while (num18 < num15)
    //                    {
    //                        num24 = 0L;
    //                        for (num11 = num18; num11 < num; num11 += num15)
    //                        {
    //                            int num26 = num11 - num21;
    //                            if (num26 >= 3)
    //                            {
    //                                int num27 = num23 + num26;
    //                                int num28 = this.Mem[num27] & 0xff;
    //                                int num29 = this.Mem[num27 - 3] & 0xff;
    //                                num25 = (num24 + num28) - num29;
    //                                int num30 = Math.Abs((int) (num25 - num24));
    //                                int num31 = Math.Abs((int) (((int) num25) - num28));
    //                                int num32 = Math.Abs((int) (((int) num25) - num29));
    //                                if ((num30 <= num31) && (num30 <= num32))
    //                                {
    //                                    num25 = num24;
    //                                }
    //                                else if (num31 <= num32)
    //                                {
    //                                    num25 = num28;
    //                                }
    //                                else
    //                                {
    //                                    num25 = num29;
    //                                }
    //                            }
    //                            else
    //                            {
    //                                num25 = num24;
    //                            }
    //                            num24 = (num25 - this.Mem[num16++]) & 0xffL;
    //                            this.Mem[num23 + num11] = (byte) (num24 & 0xffL);
    //                        }
    //                        num18++;
    //                    }
    //                    num11 = num22;
    //                    num17 = num - 2;
    //                    while (num11 < num17)
    //                    {
    //                        byte num33 = this.Mem[(num23 + num11) + 1];
    //                        this.Mem[num23 + num11] = (byte) (this.Mem[num23 + num11] + num33);
    //                        this.Mem[(num23 + num11) + 2] = (byte) (this.Mem[(num23 + num11) + 2] + num33);
    //                        num11 += 3;
    //                    }
    //                    break;
    //                }
    //                break;
    //            }
    //            case VMStandardFilters.VMSF_AUDIO:
    //                num = this.R[4];
    //                num15 = this.R[0];
    //                num16 = 0;
    //                num23 = num;
    //                this.SetValue(false, this.Mem, 0x3c020, num);
    //                if (num < 0x1e000)
    //                {
    //                    num18 = 0;
    //                    while (num18 < num15)
    //                    {
    //                        num24 = 0L;
    //                        long num34 = 0L;
    //                        long[] numArray = new long[7];
    //                        int num35 = 0;
    //                        int num36 = 0;
    //                        int num38 = 0;
    //                        int num39 = 0;
    //                        int num40 = 0;
    //                        num11 = num18;
    //                        for (int i = 0; num11 < num; i++)
    //                        {
    //                            int num37 = num36;
    //                            num36 = ((int) num34) - num35;
    //                            num35 = (int) num34;
    //                            num25 = (((8L * num24) + (num38 * num35)) + (num39 * num36)) + (num40 * num37);
    //                            num25 = Utility.URShift(num25, 3) & 0xffL;
    //                            long num42 = this.Mem[num16++];
    //                            num25 -= num42;
    //                            this.Mem[num23 + num11] = (byte) num25;
    //                            num34 = (byte) (num25 - num24);
    //                            if (num34 >= 0x80L)
    //                            {
    //                                num34 = -(0x100L - num34);
    //                            }
    //                            num24 = num25;
    //                            if (num42 >= 0x80L)
    //                            {
    //                                num42 = -(0x100L - num42);
    //                            }
    //                            int num43 = ((int) num42) << 3;
    //                            numArray[0] += Math.Abs(num43);
    //                            numArray[1] += Math.Abs((int) (num43 - num35));
    //                            numArray[2] += Math.Abs((int) (num43 + num35));
    //                            numArray[3] += Math.Abs((int) (num43 - num36));
    //                            numArray[4] += Math.Abs((int) (num43 + num36));
    //                            numArray[5] += Math.Abs((int) (num43 - num37));
    //                            numArray[6] += Math.Abs((int) (num43 + num37));
    //                            if ((i & 0x1f) == 0)
    //                            {
    //                                long num44 = numArray[0];
    //                                long num45 = 0L;
    //                                numArray[0] = 0L;
    //                                for (int j = 1; j < numArray.Length; j++)
    //                                {
    //                                    if (numArray[j] < num44)
    //                                    {
    //                                        num44 = numArray[j];
    //                                        num45 = j;
    //                                    }
    //                                    numArray[j] = 0L;
    //                                }
    //                                switch (((int) num45))
    //                                {
    //                                    case 1:
    //                                        if (num38 >= -16)
    //                                        {
    //                                            num38--;
    //                                        }
    //                                        break;

    //                                    case 2:
    //                                        if (num38 < 0x10)
    //                                        {
    //                                            num38++;
    //                                        }
    //                                        break;

    //                                    case 3:
    //                                        if (num39 >= -16)
    //                                        {
    //                                            num39--;
    //                                        }
    //                                        break;

    //                                    case 4:
    //                                        if (num39 < 0x10)
    //                                        {
    //                                            num39++;
    //                                        }
    //                                        break;

    //                                    case 5:
    //                                        if (num40 >= -16)
    //                                        {
    //                                            num40--;
    //                                        }
    //                                        break;

    //                                    case 6:
    //                                        if (num40 < 0x10)
    //                                        {
    //                                            num40++;
    //                                        }
    //                                        break;
    //                                }
    //                            }
    //                            num11 += num15;
    //                        }
    //                        num18++;
    //                    }
    //                    break;
    //                }
    //                break;

    //            case VMStandardFilters.VMSF_DELTA:
    //                num = this.R[4] & -1;
    //                num15 = this.R[0] & -1;
    //                num16 = 0;
    //                num17 = (num * 2) & -1;
    //                this.SetValue(false, this.Mem, 0x3c020, num);
    //                if (num < 0x1e000)
    //                {
    //                    for (num18 = 0; num18 < num15; num18++)
    //                    {
    //                        byte num19 = 0;
    //                        for (num20 = num + num18; num20 < num17; num20 += num15)
    //                        {
    //                            this.Mem[num20] = num19 = (byte) (num19 - this.Mem[num16++]);
    //                        }
    //                    }
    //                    break;
    //                }
    //                break;

    //            case VMStandardFilters.VMSF_UPCASE:
    //                num = this.R[4];
    //                num16 = 0;
    //                num20 = num;
    //                if (num < 0x1e000)
    //                {
    //                    while (num16 < num)
    //                    {
    //                        num6 = this.Mem[num16++];
    //                        if ((num6 == 2) && ((num6 = this.Mem[num16++]) != 2))
    //                        {
    //                            num6 = (byte) (num6 - 0x20);
    //                        }
    //                        this.Mem[num20++] = num6;
    //                    }
    //                    this.SetValue(false, this.Mem, 0x3c01c, num20 - num);
    //                    this.SetValue(false, this.Mem, 0x3c020, num);
    //                    break;
    //                }
    //                break;
    //        }
    //    }

    //    private int filterItanium_GetBits(int curPos, int bitPos, int bitCount)
    //    {
    //        int num = bitPos / 8;
    //        int bits = bitPos & 7;
    //        int number = this.Mem[curPos + num++] & 0xff;
    //        number |= (this.Mem[curPos + num++] & 0xff) << 8;
    //        number |= (this.Mem[curPos + num++] & 0xff) << 0x10;
    //        number |= (this.Mem[curPos + num] & 0xff) << 0x18;
    //        return (Utility.URShift(number, bits) & Utility.URShift(-1, (int) (0x20 - bitCount)));
    //    }

    //    private void filterItanium_SetBits(int curPos, int bitField, int bitPos, int bitCount)
    //    {
    //        int num = bitPos / 8;
    //        int num2 = bitPos & 7;
    //        int number = ~(Utility.URShift(-1, (int) (0x20 - bitCount)) << num2);
    //        bitField = bitField << num2;
    //        for (int i = 0; i < 4; i++)
    //        {
    //            this.Mem[(curPos + num) + i] = (byte) (this.Mem[(curPos + num) + i] & ((byte) number));
    //            this.Mem[(curPos + num) + i] = (byte) (this.Mem[(curPos + num) + i] | ((byte) bitField));
    //            number = Utility.URShift(number, 8) | -16777216;
    //            bitField = Utility.URShift(bitField, 8);
    //        }
    //    }

    //    private int GetOperand(VMPreparedOperand cmdOp)
    //    {
    //        int offset;
    //        if (cmdOp.Type == VMOpType.VM_OPREGMEM)
    //        {
    //            offset = (cmdOp.Offset + cmdOp.Base) & VM_MEMMASK;
    //            return Utility.readIntLittleEndian(this.Mem, offset);
    //        }
    //        offset = cmdOp.Offset;
    //        return Utility.readIntLittleEndian(this.Mem, offset);
    //    }

    //    private int GetValue(bool byteMode, byte[] mem, int offset)
    //    {
    //        if (byteMode)
    //        {
    //            if (this.IsVMMem(mem))
    //            {
    //                return mem[offset];
    //            }
    //            return (mem[offset] & 0xff);
    //        }
    //        if (this.IsVMMem(mem))
    //        {
    //            return Utility.readIntLittleEndian(mem, offset);
    //        }
    //        return Utility.readIntBigEndian(mem, offset);
    //    }

    //    internal void init()
    //    {
    //        if (this.Mem == null)
    //        {
    //            this.Mem = new byte[0x40004];
    //        }
    //    }

    //    private VMStandardFilters IsStandardFilter(byte[] code, int codeSize)
    //    {
    //        VMStandardFilterSignature[] signatureArray = new VMStandardFilterSignature[] { new VMStandardFilterSignature(0x35, 0xad576887, VMStandardFilters.VMSF_E8), new VMStandardFilterSignature(0x39, 0x3cd7e57e, VMStandardFilters.VMSF_E8E9), new VMStandardFilterSignature(120, 0x3769893f, VMStandardFilters.VMSF_ITANIUM), new VMStandardFilterSignature(0x1d, 0xe06077d, VMStandardFilters.VMSF_DELTA), new VMStandardFilterSignature(0x95, 0x1c2c5dc8, VMStandardFilters.VMSF_RGB), new VMStandardFilterSignature(0xd8, 0xbc85e701, VMStandardFilters.VMSF_AUDIO), new VMStandardFilterSignature(40, 0x46b9c560, VMStandardFilters.VMSF_UPCASE) };
    //        uint num = RarCRC.CheckCrc(uint.MaxValue, code, 0, code.Length) ^ uint.MaxValue;
    //        for (int i = 0; i < signatureArray.Length; i++)
    //        {
    //            if ((signatureArray[i].CRC == num) && (signatureArray[i].Length == code.Length))
    //            {
    //                return signatureArray[i].Type;
    //            }
    //        }
    //        return VMStandardFilters.VMSF_NONE;
    //    }

    //    private bool IsVMMem(byte[] mem)
    //    {
    //        return (this.Mem == mem);
    //    }

    //    private void optimize(VMPreparedProgram prg)
    //    {
    //        List<VMPreparedCommand> commands = prg.Commands;
    //        foreach (VMPreparedCommand command in commands)
    //        {
    //            bool flag;
    //            switch (command.OpCode)
    //            {
    //                case VMCommands.VM_MOV:
    //                {
    //                    command.OpCode = command.IsByteMode ? VMCommands.VM_MOVB : VMCommands.VM_MOVD;
    //                    continue;
    //                }
    //                case VMCommands.VM_CMP:
    //                {
    //                    command.OpCode = command.IsByteMode ? VMCommands.VM_CMPB : VMCommands.VM_CMPD;
    //                    continue;
    //                }
    //                default:
    //                    if ((VMCmdFlags.VM_CmdFlags[(int) command.OpCode] & 0x40) == 0)
    //                    {
    //                        continue;
    //                    }
    //                    flag = false;
    //                    for (int i = commands.IndexOf(command) + 1; i < commands.Count; i++)
    //                    {
    //                        int num2 = VMCmdFlags.VM_CmdFlags[(int) commands[i].OpCode];
    //                        if ((num2 & 0x38) != 0)
    //                        {
    //                            flag = true;
    //                            break;
    //                        }
    //                        if ((num2 & 0x40) != 0)
    //                        {
    //                            break;
    //                        }
    //                    }
    //                    break;
    //            }
    //            if (!flag)
    //            {
    //                switch (command.OpCode)
    //                {
    //                    case VMCommands.VM_ADD:
    //                        command.OpCode = command.IsByteMode ? VMCommands.VM_ADDB : VMCommands.VM_ADDD;
    //                        break;

    //                    case VMCommands.VM_SUB:
    //                        command.OpCode = command.IsByteMode ? VMCommands.VM_SUBB : VMCommands.VM_SUBD;
    //                        break;

    //                    case VMCommands.VM_INC:
    //                        command.OpCode = command.IsByteMode ? VMCommands.VM_INCB : VMCommands.VM_INCD;
    //                        break;

    //                    case VMCommands.VM_DEC:
    //                        command.OpCode = command.IsByteMode ? VMCommands.VM_DECB : VMCommands.VM_DECD;
    //                        break;

    //                    case VMCommands.VM_NEG:
    //                        command.OpCode = command.IsByteMode ? VMCommands.VM_NEGB : VMCommands.VM_NEGD;
    //                        break;
    //                }
    //            }
    //        }
    //    }

    //    public void prepare(byte[] code, int codeSize, VMPreparedProgram prg)
    //    {
    //        int num3;
    //        base.InitBitInput();
    //        int count = Math.Min(0x8000, codeSize);
    //        Buffer.BlockCopy(code, 0, base.InBuf, 0, count);
    //        byte num2 = 0;
    //        for (num3 = 1; num3 < codeSize; num3++)
    //        {
    //            num2 = (byte) (num2 ^ code[num3]);
    //        }
    //        base.AddBits(8);
    //        prg.CommandCount = 0;
    //        if (num2 == code[0])
    //        {
    //            VMPreparedCommand command;
    //            VMStandardFilters filters = this.IsStandardFilter(code, codeSize);
    //            if (filters != VMStandardFilters.VMSF_NONE)
    //            {
    //                command = new VMPreparedCommand();
    //                command.OpCode = VMCommands.VM_STANDARD;
    //                command.Op1.Data = (int) filters;
    //                command.Op1.Type = VMOpType.VM_OPNONE;
    //                command.Op2.Type = VMOpType.VM_OPNONE;
    //                codeSize = 0;
    //                prg.Commands.Add(command);
    //                prg.CommandCount++;
    //            }
    //            int bits = base.GetBits();
    //            base.AddBits(1);
    //            if ((bits & 0x8000) != 0)
    //            {
    //                long num5 = ReadData(this) & 0x100000000L;
    //                for (num3 = 0; (base.inAddr < codeSize) && (num3 < num5); num3++)
    //                {
    //                    prg.StaticData.Add((byte) (base.GetBits() >> 8));
    //                    base.AddBits(8);
    //                }
    //            }
    //            while (base.inAddr < codeSize)
    //            {
    //                command = new VMPreparedCommand();
    //                int num6 = base.GetBits();
    //                if ((num6 & 0x8000) == 0)
    //                {
    //                    command.OpCode = (VMCommands) (num6 >> 12);
    //                    base.AddBits(4);
    //                }
    //                else
    //                {
    //                    command.OpCode = (VMCommands) ((num6 >> 10) - 0x18);
    //                    base.AddBits(6);
    //                }
    //                if ((VMCmdFlags.VM_CmdFlags[(int) command.OpCode] & 4) != 0)
    //                {
    //                    command.IsByteMode = (base.GetBits() >> 15) == 1;
    //                    base.AddBits(1);
    //                }
    //                else
    //                {
    //                    command.IsByteMode = false;
    //                }
    //                command.Op1.Type = VMOpType.VM_OPNONE;
    //                command.Op2.Type = VMOpType.VM_OPNONE;
    //                int num7 = VMCmdFlags.VM_CmdFlags[(int) command.OpCode] & 3;
    //                if (num7 > 0)
    //                {
    //                    this.decodeArg(command.Op1, command.IsByteMode);
    //                    if (num7 == 2)
    //                    {
    //                        this.decodeArg(command.Op2, command.IsByteMode);
    //                    }
    //                    else if ((command.Op1.Type == VMOpType.VM_OPINT) && ((VMCmdFlags.VM_CmdFlags[(int) command.OpCode] & 0x18) != 0))
    //                    {
    //                        int data = command.Op1.Data;
    //                        if (data >= 0x100)
    //                        {
    //                            data -= 0x100;
    //                        }
    //                        else
    //                        {
    //                            if (data >= 0x88)
    //                            {
    //                                data -= 0x108;
    //                            }
    //                            else if (data >= 0x10)
    //                            {
    //                                data -= 8;
    //                            }
    //                            else if (data >= 8)
    //                            {
    //                                data -= 0x10;
    //                            }
    //                            data += prg.CommandCount;
    //                        }
    //                        command.Op1.Data = data;
    //                    }
    //                }
    //                prg.CommandCount++;
    //                prg.Commands.Add(command);
    //            }
    //        }
    //        VMPreparedCommand item = new VMPreparedCommand();
    //        item.OpCode = VMCommands.VM_RET;
    //        item.Op1.Type = VMOpType.VM_OPNONE;
    //        item.Op2.Type = VMOpType.VM_OPNONE;
    //        prg.Commands.Add(item);
    //        prg.CommandCount++;
    //        if (codeSize != 0)
    //        {
    //            this.optimize(prg);
    //        }
    //    }

    //    internal static int ReadData(BitInput rarVM)
    //    {
    //        int bits = rarVM.GetBits();
    //        switch ((bits & 0xc000))
    //        {
    //            case 0:
    //                rarVM.AddBits(6);
    //                return ((bits >> 10) & 15);

    //            case 0x4000:
    //                if ((bits & 0x3c00) == 0)
    //                {
    //                    bits = -256 | ((bits >> 2) & 0xff);
    //                    rarVM.AddBits(14);
    //                    return bits;
    //                }
    //                bits = (bits >> 6) & 0xff;
    //                rarVM.AddBits(10);
    //                return bits;

    //            case 0x8000:
    //                rarVM.AddBits(2);
    //                bits = rarVM.GetBits();
    //                rarVM.AddBits(0x10);
    //                return bits;
    //        }
    //        rarVM.AddBits(2);
    //        bits = rarVM.GetBits() << 0x10;
    //        rarVM.AddBits(0x10);
    //        bits |= rarVM.GetBits();
    //        rarVM.AddBits(0x10);
    //        return bits;
    //    }

    //    private bool setIP(int ip)
    //    {
    //        if (ip < this.codeSize)
    //        {
    //            if (--this.maxOpCount <= 0)
    //            {
    //                return false;
    //            }
    //            this.IP = ip;
    //        }
    //        return true;
    //    }

    //    internal void SetLowEndianValue(List<byte> mem, int offset, int value)
    //    {
    //        mem[offset] = (byte) (value & 0xff);
    //        mem[offset + 1] = (byte) (Utility.URShift(value, 8) & 0xff);
    //        mem[offset + 2] = (byte) (Utility.URShift(value, 0x10) & 0xff);
    //        mem[offset + 3] = (byte) (Utility.URShift(value, 0x18) & 0xff);
    //    }

    //    public virtual void setMemory(int pos, byte[] data, int offset, int dataSize)
    //    {
    //        if (pos < 0x40000)
    //        {
    //            for (int i = 0; i < Math.Min(data.Length - offset, dataSize); i++)
    //            {
    //                if ((0x40000 - pos) < i)
    //                {
    //                    break;
    //                }
    //                this.Mem[pos + i] = data[offset + i];
    //            }
    //        }
    //    }

    //    private void SetValue(bool byteMode, byte[] mem, int offset, int value)
    //    {
    //        if (byteMode)
    //        {
    //            if (this.IsVMMem(mem))
    //            {
    //                mem[offset] = (byte) value;
    //            }
    //            else
    //            {
    //                byte num1 = mem[offset];
    //                mem[offset] = (byte) (value & 0xff);
    //            }
    //        }
    //        else if (this.IsVMMem(mem))
    //        {
    //            Utility.WriteLittleEndian(mem, offset, value);
    //        }
    //        else
    //        {
    //            Utility.writeIntBigEndian(mem, offset, value);
    //        }
    //    }

    //    internal byte[] Mem
    //    {
    //        [CompilerGenerated]
    //        get
    //        {
    //            return this._Mem_k__BackingField;
    //        }
    //        [CompilerGenerated]
    //        private set
    //        {
    //            this._Mem_k__BackingField = value;
    //        }
    //    }
    //}


    internal class RarVM : BitInput {
        //private void  InitBlock()
        //{
        //    Mem.set_Renamed(offset + 0, Byte.valueOf((sbyte) (value_Renamed & 0xff)));
        //    Mem.set_Renamed(offset + 1, Byte.valueOf((sbyte) ((Utility.URShift(value_Renamed, 8)) & 0xff)));
        //    Mem.set_Renamed(offset + 2, Byte.valueOf((sbyte) ((Utility.URShift(value_Renamed, 16)) & 0xff)));
        //    Mem.set_Renamed(offset + 3, Byte.valueOf((sbyte) ((Utility.URShift(value_Renamed, 24)) & 0xff)));


        //}
        internal byte[] Mem { get; private set; }

        public const int VM_MEMSIZE = 0x40000;

        //UPGRADE_NOTE: Final was removed from the declaration of 'VM_MEMMASK '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int VM_MEMMASK = (VM_MEMSIZE - 1);

        public const int VM_GLOBALMEMADDR = 0x3C000;

        public const int VM_GLOBALMEMSIZE = 0x2000;

        public const int VM_FIXEDGLOBALSIZE = 64;

        private const int regCount = 8;

        private const long UINT_MASK = 0xffffFFFF; //((long)2*(long)Integer.MAX_VALUE);

        private int[] R = new int[regCount];

        private VMFlags flags;

        private int maxOpCount = 25000000;

        private int codeSize;

        private int IP;

        internal RarVM() {
            //InitBlock();
            Mem = null;
        }

        internal void init() {
            if (Mem == null) {
                Mem = new byte[VM_MEMSIZE + 4];
            }
        }

        private bool IsVMMem(byte[] mem) {
            return this.Mem == mem;
        }

        private int GetValue(bool byteMode, byte[] mem, int offset) {
            if (byteMode) {
                if (IsVMMem(mem)) {
                    return (mem[offset]);
                }
                else {
                    return (mem[offset] & 0xff);
                }
            }
            else {
                if (IsVMMem(mem)) {
                    return Utility.readIntLittleEndian(mem, offset);
                }
                else {
                    return Utility.readIntBigEndian(mem, offset);
                }
            }
        }

        private void SetValue(bool byteMode, byte[] mem, int offset, int value) {
            if (byteMode) {
                if (IsVMMem(mem)) {
                    mem[offset] = (byte)value;
                }
                else {
                    mem[offset] = (byte)((mem[offset] & 0x00) | (byte)(value & 0xff));
                }
            }
            else {
                if (IsVMMem(mem)) {
                    Utility.WriteLittleEndian(mem, offset, value);
                    //				Mem[offset + 0] = (byte) value;
                    //				Mem[offset + 1] = (byte) (value >>> 8);
                    //				Mem[offset + 2] = (byte) (value >>> 16);
                    //				Mem[offset + 3] = (byte) (value >>> 24);
                }
                else {
                    Utility.writeIntBigEndian(mem, offset, value);
                    //				Mem[offset + 3] = (byte) value;
                    //				Mem[offset + 2] = (byte) (value >>> 8);
                    //				Mem[offset + 1] = (byte) (value >>> 16);
                    //				Mem[offset + 0] = (byte) (value >>> 24);
                }
            }
            // #define SET_VALUE(ByteMode,Addr,Value) SetValue(ByteMode,(uint
            // *)Addr,Value)
        }

        internal void SetLowEndianValue(List<byte> mem, int offset, int value) {
            mem[offset + 0] = (byte)(value & 0xff);
            mem[offset + 1] = (byte)(Utility.URShift(value, 8) & 0xff);
            mem[offset + 2] = (byte)(Utility.URShift(value, 16) & 0xff);
            mem[offset + 3] = (byte)(Utility.URShift(value, 24) & 0xff);
        }

        private int GetOperand(VMPreparedOperand cmdOp) {
            int ret = 0;
            if (cmdOp.Type == VMOpType.VM_OPREGMEM) {
                int pos = (cmdOp.Offset + cmdOp.Base) & VM_MEMMASK;
                ret = Utility.readIntLittleEndian(Mem, pos);
            }
            else {
                int pos = cmdOp.Offset;
                ret = Utility.readIntLittleEndian(Mem, pos);
            }
            return ret;
        }

        public void execute(VMPreparedProgram prg) {
            for (int i = 0; i < prg.InitR.Length; i++)
            // memcpy(R,Prg->InitR,sizeof(Prg->InitR));
            {
                R[i] = prg.InitR[i];
            }

            long globalSize = (long)(Math.Min(prg.GlobalData.Count, VM_GLOBALMEMSIZE)) & 0xffFFffFF;
            if (globalSize != 0) {
                for (int i = 0; i < globalSize; i++)
                // memcpy(Mem+VM_GLOBALMEMADDR,&Prg->GlobalData[0],GlobalSize);
                {
                    Mem[VM_GLOBALMEMADDR + i] = prg.GlobalData[i];
                }
            }
            long staticSize = (long)(Math.Min(prg.StaticData.Count, VM_GLOBALMEMSIZE - globalSize)) & 0xffFFffFF;
            if (staticSize != 0) {
                for (int i = 0; i < staticSize; i++)
                // memcpy(Mem+VM_GLOBALMEMADDR+GlobalSize,&Prg->StaticData[0],StaticSize);
                {
                    Mem[VM_GLOBALMEMADDR + (int)globalSize + i] = prg.StaticData[i];
                }
            }
            R[7] = VM_MEMSIZE;
            flags = 0;

            //UPGRADE_NOTE: There is an untranslated Statement.  Please refer to original code. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1153'"
            List<VMPreparedCommand> preparedCode = prg.AltCommands.Count != 0
                                                       ? prg
                                                             .AltCommands
                                                       : prg.Commands;

            if (!ExecuteCode(preparedCode, prg.CommandCount)) {
                preparedCode[0].OpCode = VMCommands.VM_RET;
            }
            int newBlockPos = GetValue(false, Mem, VM_GLOBALMEMADDR + 0x20) & VM_MEMMASK;
            int newBlockSize = GetValue(false, Mem, VM_GLOBALMEMADDR + 0x1c) & VM_MEMMASK;
            if ((newBlockPos + newBlockSize) >= VM_MEMSIZE) {
                newBlockPos = 0;
                newBlockSize = 0;
            }

            prg.FilteredDataOffset = newBlockPos;
            prg.FilteredDataSize = newBlockSize;

            prg.GlobalData.Clear();

            int dataSize = Math.Min(GetValue(false, Mem, VM_GLOBALMEMADDR + 0x30), VM_GLOBALMEMSIZE - VM_FIXEDGLOBALSIZE);
            if (dataSize != 0) {
                //prg.GlobalData.Clear();
                // ->GlobalData.Add(dataSize+VM_FIXEDGLOBALSIZE);
                //prg.GlobalData.SetSize(dataSize + VM_FIXEDGLOBALSIZE);
                Utility.SetSize(prg.GlobalData,dataSize + VM_FIXEDGLOBALSIZE);
                for (int i = 0; i < dataSize + VM_FIXEDGLOBALSIZE; i++)
                // memcpy(&Prg->GlobalData[0],&Mem[VM_GLOBALMEMADDR],DataSize+VM_FIXEDGLOBALSIZE);
                {
                    prg.GlobalData[i] = Mem[VM_GLOBALMEMADDR + i];
                }
            }
        }

        private bool setIP(int ip) {
            if ((ip) >= codeSize) {
                return (true);
            }

            if (--maxOpCount <= 0) {
                return (false);
            }

            IP = ip;
            return true;
        }

        private bool ExecuteCode(List<VMPreparedCommand> preparedCode,
                                 int cmdCount) {
            maxOpCount = 25000000;
            this.codeSize = cmdCount;
            this.IP = 0;

            while (true) {
                VMPreparedCommand cmd = preparedCode[IP];
                int op1 = GetOperand(cmd.Op1);
                int op2 = GetOperand(cmd.Op2);
                switch (cmd.OpCode) {
                    case VMCommands.VM_MOV:
                        SetValue(cmd.IsByteMode, Mem, op1, GetValue(cmd.IsByteMode, Mem, op2));
                        // SET_VALUE(Cmd->ByteMode,Op1,GET_VALUE(Cmd->ByteMode,Op2));
                        break;

                    case VMCommands.VM_MOVB:
                        SetValue(true, Mem, op1, GetValue(true, Mem, op2));
                        break;

                    case VMCommands.VM_MOVD:
                        SetValue(false, Mem, op1, GetValue(false, Mem, op2));
                        break;


                    case VMCommands.VM_CMP: {
                            VMFlags value1 = (VMFlags)GetValue(cmd.IsByteMode, Mem, op1);
                            VMFlags result = value1 - GetValue(cmd.IsByteMode, Mem, op2);

                            if (result == 0) {
                                flags = VMFlags.VM_FZ;
                            }
                            else {
                                flags = (VMFlags)((result > value1) ? 1 : 0 | (int)(result & VMFlags.VM_FS));
                            }
                        }
                        break;


                    case VMCommands.VM_CMPB: {
                            VMFlags value1 = (VMFlags)GetValue(true, Mem, op1);
                            VMFlags result = value1 - GetValue(true, Mem, op2);
                            if (result == 0) {
                                flags = VMFlags.VM_FZ;
                            }
                            else {
                                flags = (VMFlags)((result > value1) ? 1 : 0 | (int)(result & VMFlags.VM_FS));
                            }
                        }
                        break;

                    case VMCommands.VM_CMPD: {
                            VMFlags value1 = (VMFlags)GetValue(false, Mem, op1);
                            VMFlags result = value1 - GetValue(false, Mem, op2);
                            if (result == 0) {
                                flags = VMFlags.VM_FZ;
                            }
                            else {
                                flags = (VMFlags)((result > value1) ? 1 : 0 | (int)(result & VMFlags.VM_FS));
                            }
                        }
                        break;


                    case VMCommands.VM_ADD: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            int result =
                                (int)
                                ((((long)value1 + (long)GetValue(cmd.IsByteMode, Mem, op2))) &
                                 unchecked((int)0xffffffff));
                            if (cmd.IsByteMode) {
                                result &= 0xff;
                                flags =
                                    (VMFlags)
                                    ((result < value1)
                                         ? 1
                                         : 0 |
                                           (result == 0
                                                ? (int)VMFlags.VM_FZ
                                                : (((result & 0x80) != 0) ? (int)VMFlags.VM_FS : 0)));
                                // Flags=(Result<Value1)|(Result==0 ? VM_FZ:((Result&0x80) ?
                                // VM_FS:0));
                            }
                            else
                                flags =
                                    (VMFlags)
                                    ((result < value1)
                                         ? 1
                                         : 0 | (result == 0 ? (int)VMFlags.VM_FZ : (result & (int)VMFlags.VM_FS)));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;


                    case VMCommands.VM_ADDB:
                        SetValue(true, Mem, op1,
                                 (int)
                                 ((long)GetValue(true, Mem, op1) & 0xFFffFFff + (long)GetValue(true, Mem, op2) &
                                  unchecked((int)0xFFffFFff)));
                        break;

                    case VMCommands.VM_ADDD:
                        SetValue(false, Mem, op1,
                                 (int)
                                 ((long)GetValue(false, Mem, op1) & 0xFFffFFff + (long)GetValue(false, Mem, op2) &
                                  unchecked((int)0xFFffFFff)));
                        break;


                    case VMCommands.VM_SUB: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            int result =
                                (int)
                                ((long)value1 & 0xffFFffFF - (long)GetValue(cmd.IsByteMode, Mem, op2) &
                                 unchecked((int)0xFFffFFff));
                            flags =
                                (VMFlags)
                                ((result == 0)
                                     ? (int)VMFlags.VM_FZ
                                     : ((result > value1) ? 1 : 0 | (result & (int)VMFlags.VM_FS)));
                            SetValue(cmd.IsByteMode, Mem, op1, result); // (Cmd->ByteMode,Op1,Result);
                        }
                        break;


                    case VMCommands.VM_SUBB:
                        SetValue(true, Mem, op1,
                                 (int)
                                 ((long)GetValue(true, Mem, op1) & 0xFFffFFff - (long)GetValue(true, Mem, op2) &
                                  unchecked((int)0xFFffFFff)));
                        break;

                    case VMCommands.VM_SUBD:
                        SetValue(false, Mem, op1,
                                 (int)
                                 ((long)GetValue(false, Mem, op1) & 0xFFffFFff - (long)GetValue(false, Mem, op2) &
                                  unchecked((int)0xFFffFFff)));
                        break;


                    case VMCommands.VM_JZ:
                        if ((flags & VMFlags.VM_FZ) != 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_JNZ:
                        if ((flags & VMFlags.VM_FZ) == 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_INC: {
                            int result = (int)((long)GetValue(cmd.IsByteMode, Mem, op1) & 0xFFffFFffL + 1L);
                            if (cmd.IsByteMode) {
                                result &= 0xff;
                            }

                            SetValue(cmd.IsByteMode, Mem, op1, result);
                            flags = (VMFlags)(result == 0 ? (int)VMFlags.VM_FZ : result & (int)VMFlags.VM_FS);
                        }
                        break;


                    case VMCommands.VM_INCB:
                        SetValue(true, Mem, op1, (int)((long)GetValue(true, Mem, op1) & 0xFFffFFffL + 1L));
                        break;

                    case VMCommands.VM_INCD:
                        SetValue(false, Mem, op1, (int)((long)GetValue(false, Mem, op1) & 0xFFffFFffL + 1L));
                        break;


                    case VMCommands.VM_DEC: {
                            int result = (int)((long)GetValue(cmd.IsByteMode, Mem, op1) & 0xFFffFFff - 1);
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                            flags = (VMFlags)(result == 0 ? (int)VMFlags.VM_FZ : result & (int)VMFlags.VM_FS);
                        }
                        break;


                    case VMCommands.VM_DECB:
                        SetValue(true, Mem, op1, (int)((long)GetValue(true, Mem, op1) & 0xFFffFFff - 1));
                        break;

                    case VMCommands.VM_DECD:
                        SetValue(false, Mem, op1, (int)((long)GetValue(false, Mem, op1) & 0xFFffFFff - 1));
                        break;


                    case VMCommands.VM_JMP:
                        setIP(GetValue(false, Mem, op1));
                        continue;

                    case VMCommands.VM_XOR: {
                            int result = GetValue(cmd.IsByteMode, Mem, op1) ^ GetValue(cmd.IsByteMode, Mem, op2);
                            flags = (VMFlags)(result == 0 ? (int)VMFlags.VM_FZ : result & (int)VMFlags.VM_FS);
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_AND: {
                            int result = GetValue(cmd.IsByteMode, Mem, op1) & GetValue(cmd.IsByteMode, Mem, op2);
                            flags = (VMFlags)(result == 0 ? (int)VMFlags.VM_FZ : result & (int)VMFlags.VM_FS);
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_OR: {
                            int result = GetValue(cmd.IsByteMode, Mem, op1) | GetValue(cmd.IsByteMode, Mem, op2);
                            flags = (VMFlags)(result == 0 ? (int)VMFlags.VM_FZ : result & (int)VMFlags.VM_FS);
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_TEST: {
                            int result = GetValue(cmd.IsByteMode, Mem, op1) & GetValue(cmd.IsByteMode, Mem, op2);
                            flags = (VMFlags)(result == 0 ? (int)VMFlags.VM_FZ : result & (int)VMFlags.VM_FS);
                        }
                        break;

                    case VMCommands.VM_JS:
                        if ((flags & VMFlags.VM_FS) != 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_JNS:
                        if ((flags & VMFlags.VM_FS) == 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_JB:
                        if ((flags & VMFlags.VM_FC) != 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_JBE:
                        if ((flags & (VMFlags.VM_FC | VMFlags.VM_FZ)) != 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_JA:
                        if ((flags & (VMFlags.VM_FC | VMFlags.VM_FZ)) == 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_JAE:
                        if ((flags & VMFlags.VM_FC) == 0) {
                            setIP(GetValue(false, Mem, op1));
                            continue;
                        }
                        break;

                    case VMCommands.VM_PUSH:
                        R[7] -= 4;
                        SetValue(false, Mem, R[7] & VM_MEMMASK, GetValue(false, Mem, op1));
                        break;

                    case VMCommands.VM_POP:
                        SetValue(false, Mem, op1, GetValue(false, Mem, R[7] & VM_MEMMASK));
                        R[7] += 4;
                        break;

                    case VMCommands.VM_CALL:
                        R[7] -= 4;
                        SetValue(false, Mem, R[7] & VM_MEMMASK, IP + 1);
                        setIP(GetValue(false, Mem, op1));
                        continue;

                    case VMCommands.VM_NOT:
                        SetValue(cmd.IsByteMode, Mem, op1, ~GetValue(cmd.IsByteMode, Mem, op1));
                        break;

                    case VMCommands.VM_SHL: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            int value2 = GetValue(cmd.IsByteMode, Mem, op2);
                            int result = value1 << value2;
                            flags =
                                (VMFlags)
                                ((result == 0 ? (int)VMFlags.VM_FZ : (result & (int)VMFlags.VM_FS)) |
                                 (((value1 << (value2 - 1)) & unchecked((int)0x80000000)) != 0
                                      ? (int)VMFlags.VM_FC
                                      : 0));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_SHR: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            int value2 = GetValue(cmd.IsByteMode, Mem, op2);
                            int result = Utility.URShift(value1, value2);
                            flags =
                                (VMFlags)
                                ((result == 0 ? (int)VMFlags.VM_FZ : (result & (int)VMFlags.VM_FS)) |
                                 ((Utility.URShift(value1, (value2 - 1))) & (int)VMFlags.VM_FC));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_SAR: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            int value2 = GetValue(cmd.IsByteMode, Mem, op2);
                            int result = ((int)value1) >> value2;
                            flags =
                                (VMFlags)
                                ((result == 0 ? (int)VMFlags.VM_FZ : (result & (int)VMFlags.VM_FS)) |
                                 ((value1 >> (value2 - 1)) & (int)VMFlags.VM_FC));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_NEG: {
                            int result = -GetValue(cmd.IsByteMode, Mem, op1);
                            flags =
                                (VMFlags)
                                (result == 0
                                     ? (int)VMFlags.VM_FZ
                                     : (int)VMFlags.VM_FC | (result & (int)VMFlags.VM_FS));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;


                    case VMCommands.VM_NEGB:
                        SetValue(true, Mem, op1, -GetValue(true, Mem, op1));
                        break;

                    case VMCommands.VM_NEGD:
                        SetValue(false, Mem, op1, -GetValue(false, Mem, op1));
                        break;

                    case VMCommands.VM_PUSHA: {
                            for (int i = 0, SP = R[7] - 4; i < regCount; i++, SP -= 4) {
                                SetValue(false, Mem, SP & VM_MEMMASK, R[i]);
                            }
                            R[7] -= regCount * 4;
                        }
                        break;

                    case VMCommands.VM_POPA: {
                            for (int i = 0, SP = R[7]; i < regCount; i++, SP += 4)
                                R[7 - i] = GetValue(false, Mem, SP & VM_MEMMASK);
                        }
                        break;

                    case VMCommands.VM_PUSHF:
                        R[7] -= 4;
                        SetValue(false, Mem, R[7] & VM_MEMMASK, (int)flags);
                        break;

                    case VMCommands.VM_POPF:
                        flags = (VMFlags)GetValue(false, Mem, R[7] & VM_MEMMASK);
                        R[7] += 4;
                        break;

                    case VMCommands.VM_MOVZX:
                        SetValue(false, Mem, op1, GetValue(true, Mem, op2));
                        break;

                    case VMCommands.VM_MOVSX:
                        SetValue(false, Mem, op1, (byte)GetValue(true, Mem, op2));
                        break;

                    case VMCommands.VM_XCHG: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            SetValue(cmd.IsByteMode, Mem, op1, GetValue(cmd.IsByteMode, Mem, op2));
                            SetValue(cmd.IsByteMode, Mem, op2, value1);
                        }
                        break;

                    case VMCommands.VM_MUL: {
                            int result =
                                (int)
                                (((long)GetValue(cmd.IsByteMode, Mem, op1) &
                                  0xFFffFFff * (long)GetValue(cmd.IsByteMode, Mem, op2) & unchecked((int)0xFFffFFff)) &
                                 unchecked((int)0xFFffFFff));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_DIV: {
                            int divider = GetValue(cmd.IsByteMode, Mem, op2);
                            if (divider != 0) {
                                int result = GetValue(cmd.IsByteMode, Mem, op1) / divider;
                                SetValue(cmd.IsByteMode, Mem, op1, result);
                            }
                        }
                        break;

                    case VMCommands.VM_ADC: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            int FC = (int)(flags & VMFlags.VM_FC);
                            int result =
                                (int)
                                ((long)value1 & 0xFFffFFff + (long)GetValue(cmd.IsByteMode, Mem, op2) &
                                 0xFFffFFff + (long)FC & unchecked((int)0xFFffFFff));
                            if (cmd.IsByteMode) {
                                result &= 0xff;
                            }

                            flags =
                                (VMFlags)
                                ((result < value1 || result == value1 && FC != 0)
                                     ? 1
                                     : 0 | (result == 0 ? (int)VMFlags.VM_FZ : (result & (int)VMFlags.VM_FS)));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;

                    case VMCommands.VM_SBB: {
                            int value1 = GetValue(cmd.IsByteMode, Mem, op1);
                            int FC = (int)(flags & VMFlags.VM_FC);
                            int result =
                                (int)
                                ((long)value1 & 0xFFffFFff - (long)GetValue(cmd.IsByteMode, Mem, op2) &
                                 0xFFffFFff - (long)FC & unchecked((int)0xFFffFFff));
                            if (cmd.IsByteMode) {
                                result &= 0xff;
                            }
                            flags =
                                (VMFlags)
                                ((result > value1 || result == value1 && FC != 0)
                                     ? 1
                                     : 0 | (result == 0 ? (int)VMFlags.VM_FZ : (result & (int)VMFlags.VM_FS)));
                            SetValue(cmd.IsByteMode, Mem, op1, result);
                        }
                        break;


                    case VMCommands.VM_RET:
                        if (R[7] >= VM_MEMSIZE) {
                            return (true);
                        }
                        setIP(GetValue(false, Mem, R[7] & VM_MEMMASK));
                        R[7] += 4;
                        continue;


                    case VMCommands.VM_STANDARD:
                        ExecuteStandardFilter((VMStandardFilters)(cmd.Op1.Data));
                        break;

                    case VMCommands.VM_PRINT:
                        break;
                }
                IP++;
                --maxOpCount;
            }
        }

        public void prepare(byte[] code, int codeSize, VMPreparedProgram prg) {
            InitBitInput();
            int cpLength = System.Math.Min(MAX_SIZE, codeSize);
            // memcpy(inBuf,Code,Min(CodeSize,BitInput::MAX_SIZE));
#if !PORTABLE
            Buffer.BlockCopy(code, 0, InBuf, 0, cpLength);
#else
            Array.Copy(code, 0, InBuf, 0, cpLength);
#endif
            byte xorSum = 0;
            for (int i = 1; i < codeSize; i++) {
                xorSum ^= code[i];
            }

            AddBits(8);

            prg.CommandCount = 0;
            if (xorSum == code[0]) {
                VMStandardFilters filterType = IsStandardFilter(code, codeSize);
                if (filterType != VMStandardFilters.VMSF_NONE) {
                    VMPreparedCommand curCmd = new VMPreparedCommand();
                    curCmd.OpCode = VMCommands.VM_STANDARD;
                    curCmd.Op1.Data = (int)filterType;
                    curCmd.Op1.Type = VMOpType.VM_OPNONE;
                    curCmd.Op2.Type = VMOpType.VM_OPNONE;
                    codeSize = 0;
                    prg.Commands.Add(curCmd);
                    prg.CommandCount = prg.CommandCount + 1;
                    // TODO
                    // curCmd->Op1.Data=FilterType;
                    // >>>>>> CurCmd->Op1.Addr=&CurCmd->Op1.Data; <<<<<<<<<< not set
                    // do i need to ?
                    // >>>>>> CurCmd->Op2.Addr=&CurCmd->Op2.Data; <<<<<<<<<< "
                    // CurCmd->Op1.Type=CurCmd->Op2.Type=VM_OPNONE;
                    // CodeSize=0;
                }
                int dataFlag = GetBits();
                AddBits(1);

                // Read static data contained in DB operators. This data cannot be
                // changed,
                // it is a part of VM code, not a filter parameter.

                if ((dataFlag & 0x8000) != 0) {
                    long dataSize = (long)((long)ReadData(this) & 0xffFFffFFL + 1L);
                    for (int i = 0; inAddr < codeSize && i < dataSize; i++) {
                        prg.StaticData.Add((byte)(GetBits() >> 8));
                        AddBits(8);
                    }
                }

                while (inAddr < codeSize) {
                    VMPreparedCommand curCmd = new VMPreparedCommand();
                    int data = GetBits();
                    if ((data & 0x8000) == 0) {
                        curCmd.OpCode = (VMCommands)((data >> 12));
                        AddBits(4);
                    }
                    else {
                        curCmd.OpCode = (VMCommands)((data >> 10) - 24);
                        AddBits(6);
                    }
                    if ((VMCmdFlags.VM_CmdFlags[(int)curCmd.OpCode] & VMCmdFlags.VMCF_BYTEMODE) != 0) {
                        curCmd.IsByteMode = (GetBits() >> 15) == 1 ? true : false;
                        AddBits(1);
                    }
                    else {
                        curCmd.IsByteMode = false;
                    }
                    curCmd.Op1.Type = VMOpType.VM_OPNONE;
                    curCmd.Op2.Type = VMOpType.VM_OPNONE;

                    int opNum = (VMCmdFlags.VM_CmdFlags[(int)curCmd.OpCode] & VMCmdFlags.VMCF_OPMASK);
                    // TODO >>> CurCmd->Op1.Addr=CurCmd->Op2.Addr=NULL; <<<???
                    if (opNum > 0) {
                        decodeArg(curCmd.Op1, curCmd.IsByteMode);
                        if (opNum == 2)
                            decodeArg(curCmd.Op2, curCmd.IsByteMode);
                        else {
                            if (curCmd.Op1.Type == VMOpType.VM_OPINT &&
                                (VMCmdFlags.VM_CmdFlags[(int)curCmd.OpCode] &
                                 (VMCmdFlags.VMCF_JUMP | VMCmdFlags.VMCF_PROC)) != 0) {
                                int distance = curCmd.Op1.Data;
                                if (distance >= 256)
                                    distance -= 256;
                                else {
                                    if (distance >= 136) {
                                        distance -= 264;
                                    }
                                    else {
                                        if (distance >= 16) {
                                            distance -= 8;
                                        }
                                        else {
                                            if (distance >= 8) {
                                                distance -= 16;
                                            }
                                        }
                                    }
                                    distance += prg.CommandCount;
                                }
                                curCmd.Op1.Data = distance;
                            }
                        }
                    }
                    prg.CommandCount = (prg.CommandCount + 1);
                    prg.Commands.Add(curCmd);
                }
            }
            VMPreparedCommand curCmd2 = new VMPreparedCommand();
            curCmd2.OpCode = VMCommands.VM_RET;
            // TODO CurCmd->Op1.Addr=&CurCmd->Op1.Data;
            // CurCmd->Op2.Addr=&CurCmd->Op2.Data;
            curCmd2.Op1.Type = VMOpType.VM_OPNONE;
            curCmd2.Op2.Type = VMOpType.VM_OPNONE;

            // for (int i=0;i<prg.CmdCount;i++)
            // {
            // VM_PreparedCommand *Cmd=&Prg->Cmd[I];
            // if (Cmd->Op1.Addr==NULL)
            // Cmd->Op1.Addr=&Cmd->Op1.Data;
            // if (Cmd->Op2.Addr==NULL)
            // Cmd->Op2.Addr=&Cmd->Op2.Data;
            // }

            prg.Commands.Add(curCmd2);
            prg.CommandCount = prg.CommandCount + 1;
            // #ifdef VM_OPTIMIZE
            if (codeSize != 0) {
                optimize(prg);
            }
        }

        private void decodeArg(VMPreparedOperand op, bool byteMode) {
            int data = GetBits();
            if ((data & 0x8000) != 0) {
                op.Type = VMOpType.VM_OPREG;
                op.Data = (data >> 12) & 7;
                op.Offset = op.Data;
                AddBits(4);
            }
            else {
                if ((data & 0xc000) == 0) {
                    op.Type = VMOpType.VM_OPINT;
                    if (byteMode) {
                        op.Data = (data >> 6) & 0xff;
                        AddBits(10);
                    }
                    else {
                        AddBits(2);
                        op.Data = ReadData(this);
                    }
                }
                else {
                    op.Type = VMOpType.VM_OPREGMEM;
                    if ((data & 0x2000) == 0) {
                        op.Data = (data >> 10) & 7;
                        op.Offset = op.Data;
                        op.Base = 0;
                        AddBits(6);
                    }
                    else {
                        if ((data & 0x1000) == 0) {
                            op.Data = (data >> 9) & 7;
                            op.Offset = op.Data;
                            AddBits(7);
                        }
                        else {
                            op.Data = 0;
                            AddBits(4);
                        }
                        op.Base = ReadData(this);
                    }
                }
            }
        }

        private void optimize(VMPreparedProgram prg) {
            //UPGRADE_NOTE: There is an untranslated Statement.  Please refer to original code. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1153'"

            List<VMPreparedCommand> commands = prg.Commands;
            //UPGRADE_ISSUE: The following fragment of code could not be parsed and was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1156'"
            foreach (VMPreparedCommand cmd in commands) {
                switch (cmd.OpCode) {
                    case VMCommands.VM_MOV:
                        cmd.OpCode = cmd.IsByteMode ? VMCommands.VM_MOVB : VMCommands.VM_MOVD;
                        continue;

                    case VMCommands.VM_CMP:
                        cmd.OpCode = cmd.IsByteMode ? VMCommands.VM_CMPB : VMCommands.VM_CMPD;
                        continue;
                }
                if ((VMCmdFlags.VM_CmdFlags[(int)cmd.OpCode] & VMCmdFlags.VMCF_CHFLAGS) == 0) {
                    continue;
                }
                bool flagsRequired = false;

                for (int i = commands.IndexOf(cmd) + 1; i < commands.Count; i++) {
                    int flags = VMCmdFlags.VM_CmdFlags[(int)commands[i].OpCode];
                    if ((flags & (VMCmdFlags.VMCF_JUMP | VMCmdFlags.VMCF_PROC | VMCmdFlags.VMCF_USEFLAGS)) != 0) {
                        flagsRequired = true;
                        break;
                    }
                    if ((flags & VMCmdFlags.VMCF_CHFLAGS) != 0) {
                        break;
                    }
                }
                if (flagsRequired) {
                    continue;
                }
                switch (cmd.OpCode) {
                    case VMCommands.VM_ADD:
                        cmd.OpCode = cmd.IsByteMode ? VMCommands.VM_ADDB : VMCommands.VM_ADDD;
                        continue;

                    case VMCommands.VM_SUB:
                        cmd.OpCode = cmd.IsByteMode ? VMCommands.VM_SUBB : VMCommands.VM_SUBD;
                        continue;

                    case VMCommands.VM_INC:
                        cmd.OpCode = cmd.IsByteMode ? VMCommands.VM_INCB : VMCommands.VM_INCD;
                        continue;

                    case VMCommands.VM_DEC:
                        cmd.OpCode = cmd.IsByteMode ? VMCommands.VM_DECB : VMCommands.VM_DECD;
                        continue;

                    case VMCommands.VM_NEG:
                        cmd.OpCode = cmd.IsByteMode ? VMCommands.VM_NEGB : VMCommands.VM_NEGD;
                        continue;
                }
            }
        }

        internal static int ReadData(BitInput rarVM) {
            int data = rarVM.GetBits();
            switch (data & 0xc000) {
                case 0:
                    rarVM.AddBits(6);
                    return ((data >> 10) & 0xf);

                case 0x4000:
                    if ((data & 0x3c00) == 0) {
                        data = unchecked((int)0xffffff00) | ((data >> 2) & 0xff);
                        rarVM.AddBits(14);
                    }
                    else {
                        data = (data >> 6) & 0xff;
                        rarVM.AddBits(10);
                    }
                    return (data);

                case 0x8000:
                    rarVM.AddBits(2);
                    data = rarVM.GetBits();
                    rarVM.AddBits(16);
                    return (data);

                default:
                    rarVM.AddBits(2);
                    data = (rarVM.GetBits() << 16);
                    rarVM.AddBits(16);
                    data |= rarVM.GetBits();
                    rarVM.AddBits(16);
                    return (data);
            }
        }

        private VMStandardFilters IsStandardFilter(byte[] code, int codeSize) {
            VMStandardFilterSignature[] stdList = new VMStandardFilterSignature[]
                                                      {
                                                          new VMStandardFilterSignature(53, 0xad576887,
                                                                                        VMStandardFilters.VMSF_E8),
                                                          new VMStandardFilterSignature(57, 0x3cd7e57e,
                                                                                        VMStandardFilters.VMSF_E8E9),
                                                          new VMStandardFilterSignature(120, 0x3769893f,
                                                                                        VMStandardFilters.VMSF_ITANIUM),
                                                          new VMStandardFilterSignature(29, 0x0e06077d,
                                                                                        VMStandardFilters.VMSF_DELTA),
                                                          new VMStandardFilterSignature(149, 0x1c2c5dc8,
                                                                                        VMStandardFilters.VMSF_RGB),
                                                          new VMStandardFilterSignature(216, 0xbc85e701,
                                                                                        VMStandardFilters.VMSF_AUDIO),
                                                          new VMStandardFilterSignature(40, 0x46b9c560,
                                                                                        VMStandardFilters.VMSF_UPCASE)
                                                      };
            uint CodeCRC = RarCRC.CheckCrc(0xffffffff, code, 0, code.Length) ^ 0xffffffff;
            for (int i = 0; i < stdList.Length; i++) {
                if (stdList[i].CRC == CodeCRC && stdList[i].Length == code.Length) {
                    return (stdList[i].Type);
                }
            }
            return (VMStandardFilters.VMSF_NONE);
        }

        private void ExecuteStandardFilter(VMStandardFilters filterType) {
            switch (filterType) {
                case VMStandardFilters.VMSF_E8:
                case VMStandardFilters.VMSF_E8E9: {
                        int dataSize = R[4];
                        long fileOffset = R[6] & unchecked((int)0xFFffFFff);

                        if (dataSize >= VM_GLOBALMEMADDR) {
                            break;
                        }
                        int fileSize = 0x1000000;
                        byte cmpByte2 = (byte)((filterType == VMStandardFilters.VMSF_E8E9) ? 0xe9 : 0xe8);
                        for (int curPos = 0; curPos < dataSize - 4; ) {
                            byte curByte = Mem[curPos++];
                            if (curByte == 0xe8 || curByte == cmpByte2) {
                                //		#ifdef PRESENT_INT32
                                //		            sint32 Offset=CurPos+FileOffset;
                                //		            sint32 Addr=GET_VALUE(false,Data);
                                //		            if (Addr<0)
                                //		            {
                                //		              if (Addr+Offset>=0)
                                //		                SET_VALUE(false,Data,Addr+FileSize);
                                //		            }
                                //		            else
                                //		              if (Addr<FileSize)
                                //		                SET_VALUE(false,Data,Addr-Offset);
                                //		#else
                                long offset = curPos + fileOffset;
                                long Addr = GetValue(false, Mem, curPos);
                                if ((Addr & unchecked((int)0x80000000)) != 0) {
                                    if (((Addr + offset) & unchecked((int)0x80000000)) == 0)
                                        SetValue(false, Mem, curPos, (int)Addr + fileSize);
                                }
                                else {
                                    if (((Addr - fileSize) & unchecked((int)0x80000000)) != 0) {
                                        SetValue(false, Mem, curPos, (int)(Addr - offset));
                                    }
                                }
                                //		#endif
                                curPos += 4;
                            }
                        }
                    }
                    break;

                case VMStandardFilters.VMSF_ITANIUM: {
                        int dataSize = R[4];
                        long fileOffset = R[6] & unchecked((int)0xFFffFFff);

                        if (dataSize >= VM_GLOBALMEMADDR) {
                            break;
                        }
                        int curPos = 0;
                        //UPGRADE_NOTE: Final was removed from the declaration of 'Masks '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
                        byte[] Masks = new byte[] { 4, 4, 6, 6, 0, 0, 7, 7, 4, 4, 0, 0, 4, 4, 0, 0 };
                        fileOffset = Utility.URShift(fileOffset, 4);

                        while (curPos < dataSize - 21) {
                            int Byte = (Mem[curPos] & 0x1f) - 0x10;
                            if (Byte >= 0) {
                                byte cmdMask = Masks[Byte];
                                if (cmdMask != 0)
                                    for (int i = 0; i <= 2; i++)
                                        if ((cmdMask & (1 << i)) != 0) {
                                            int startPos = i * 41 + 5;
                                            int opType = filterItanium_GetBits(curPos, startPos + 37, 4);
                                            if (opType == 5) {
                                                int offset = filterItanium_GetBits(curPos, startPos + 13, 20);
                                                filterItanium_SetBits(curPos, (int)(offset - fileOffset) & 0xfffff,
                                                                      startPos + 13, 20);
                                            }
                                        }
                            }
                            curPos += 16;
                            fileOffset++;
                        }
                    }
                    break;

                case VMStandardFilters.VMSF_DELTA: {
                        int dataSize = R[4] & unchecked((int)0xFFffFFff);
                        int channels = R[0] & unchecked((int)0xFFffFFff);
                        int srcPos = 0;
                        int border = (dataSize * 2) & unchecked((int)0xFFffFFff);
                        SetValue(false, Mem, VM_GLOBALMEMADDR + 0x20, (int)dataSize);
                        if (dataSize >= VM_GLOBALMEMADDR / 2) {
                            break;
                        }
                        //		 bytes from same channels are grouped to continual data blocks,
                        //		 so we need to place them back to their interleaving positions

                        for (int curChannel = 0; curChannel < channels; curChannel++) {
                            byte PrevByte = 0;
                            for (int destPos = dataSize + curChannel; destPos < border; destPos += channels) {
                                Mem[destPos] = (PrevByte = (byte)(PrevByte - Mem[srcPos++]));
                            }
                        }
                    }
                    break;

                case VMStandardFilters.VMSF_RGB: {
                        // byte *SrcData=Mem,*DestData=SrcData+DataSize;
                        int dataSize = R[4], width = R[0] - 3, posR = R[1];
                        int channels = 3;
                        int srcPos = 0;
                        int destDataPos = dataSize;
                        SetValue(false, Mem, VM_GLOBALMEMADDR + 0x20, dataSize);
                        if (dataSize >= VM_GLOBALMEMADDR / 2 || posR < 0) {
                            break;
                        }
                        for (int curChannel = 0; curChannel < channels; curChannel++) {
                            long prevByte = 0;

                            for (int i = curChannel; i < dataSize; i += channels) {
                                long predicted;
                                int upperPos = i - width;
                                if (upperPos >= 3) {
                                    int upperDataPos = destDataPos + upperPos;
                                    int upperByte = Mem[(int)upperDataPos] & 0xff;
                                    int upperLeftByte = Mem[upperDataPos - 3] & 0xff;
                                    predicted = prevByte + upperByte - upperLeftByte;
                                    int pa = System.Math.Abs((int)(predicted - prevByte));
                                    int pb = System.Math.Abs((int)(predicted - upperByte));
                                    int pc = System.Math.Abs((int)(predicted - upperLeftByte));
                                    if (pa <= pb && pa <= pc) {
                                        predicted = prevByte;
                                    }
                                    else {
                                        if (pb <= pc) {
                                            predicted = upperByte;
                                        }
                                        else {
                                            predicted = upperLeftByte;
                                        }
                                    }
                                }
                                else {
                                    predicted = prevByte;
                                }

                                prevByte = (predicted - Mem[srcPos++] & 0xff) & 0xff;
                                Mem[destDataPos + i] = (byte)(prevByte & 0xff);
                            }
                        }
                        for (int i = posR, border = dataSize - 2; i < border; i += 3) {
                            byte G = Mem[destDataPos + i + 1];
                            Mem[destDataPos + i] = (byte)(Mem[destDataPos + i] + G);
                            Mem[destDataPos + i + 2] = (byte)(Mem[destDataPos + i + 2] + G);
                        }
                    }
                    break;

                case VMStandardFilters.VMSF_AUDIO: {
                        int dataSize = R[4], channels = R[0];
                        int srcPos = 0;
                        int destDataPos = dataSize;
                        //byte *SrcData=Mem,*DestData=SrcData+DataSize;
                        SetValue(false, Mem, VM_GLOBALMEMADDR + 0x20, dataSize);
                        if (dataSize >= VM_GLOBALMEMADDR / 2) {
                            break;
                        }
                        for (int curChannel = 0; curChannel < channels; curChannel++) {
                            long prevByte = 0;
                            long prevDelta = 0;
                            long[] Dif = new long[7];
                            int D1 = 0, D2 = 0, D3;
                            int K1 = 0, K2 = 0, K3 = 0;

                            for (int i = curChannel, byteCount = 0; i < dataSize; i += channels, byteCount++) {
                                D3 = D2;
                                D2 = (int)(prevDelta - D1);
                                D1 = (int)prevDelta;

                                long predicted = 8 * prevByte + K1 * D1 + K2 * D2 + K3 * D3;
                                predicted = Utility.URShift(predicted, 3) & 0xff;

                                long curByte = (long)(Mem[srcPos++]);

                                predicted -= curByte;
                                Mem[destDataPos + i] = (byte)predicted;
                                prevDelta = (byte)(predicted - prevByte);
                                //fix java byte
                                if (prevDelta >= 128) {
                                    prevDelta = 0 - (256 - prevDelta);
                                }
                                prevByte = predicted;
                                //fix java byte
                                if (curByte >= 128) {
                                    curByte = 0 - (256 - curByte);
                                }
                                int D = ((int)curByte) << 3;

                                Dif[0] += System.Math.Abs(D);
                                Dif[1] += System.Math.Abs(D - D1);
                                Dif[2] += System.Math.Abs(D + D1);
                                Dif[3] += System.Math.Abs(D - D2);
                                Dif[4] += System.Math.Abs(D + D2);
                                Dif[5] += System.Math.Abs(D - D3);
                                Dif[6] += System.Math.Abs(D + D3);

                                if ((byteCount & 0x1f) == 0) {
                                    long minDif = Dif[0], numMinDif = 0;
                                    Dif[0] = 0;
                                    for (int j = 1; j < Dif.Length; j++) {
                                        if (Dif[j] < minDif) {
                                            minDif = Dif[j];
                                            numMinDif = j;
                                        }
                                        Dif[j] = 0;
                                    }
                                    switch ((int)numMinDif) {
                                        case 1:
                                            if (K1 >= -16)
                                                K1--;
                                            break;

                                        case 2:
                                            if (K1 < 16)
                                                K1++;
                                            break;

                                        case 3:
                                            if (K2 >= -16)
                                                K2--;
                                            break;

                                        case 4:
                                            if (K2 < 16)
                                                K2++;
                                            break;

                                        case 5:
                                            if (K3 >= -16)
                                                K3--;
                                            break;

                                        case 6:
                                            if (K3 < 16)
                                                K3++;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    break;

                case VMStandardFilters.VMSF_UPCASE: {
                        int dataSize = R[4], srcPos = 0, destPos = dataSize;
                        if (dataSize >= VM_GLOBALMEMADDR / 2) {
                            break;
                        }
                        while (srcPos < dataSize) {
                            byte curByte = Mem[srcPos++];
                            if (curByte == 2 && (curByte = Mem[srcPos++]) != 2) {
                                curByte = (byte)(curByte - 32);
                            }
                            Mem[destPos++] = curByte;
                        }
                        SetValue(false, Mem, VM_GLOBALMEMADDR + 0x1c, destPos - dataSize);
                        SetValue(false, Mem, VM_GLOBALMEMADDR + 0x20, dataSize);
                    }
                    break;
            }
        }

        private void filterItanium_SetBits(int curPos, int bitField, int bitPos, int bitCount) {
            int inAddr = bitPos / 8;
            int inBit = bitPos & 7;
            int andMask = Utility.URShift(unchecked((int)0xffffffff), (32 - bitCount));
            andMask = ~(andMask << inBit);

            bitField <<= inBit;

            for (int i = 0; i < 4; i++) {
                Mem[curPos + inAddr + i] &= (byte)(andMask);
                Mem[curPos + inAddr + i] |= (byte)(bitField);
                andMask = (Utility.URShift(andMask, 8)) | unchecked((int)0xff000000);
                bitField = Utility.URShift(bitField, 8);
            }
        }

        private int filterItanium_GetBits(int curPos, int bitPos, int bitCount) {
            int inAddr = bitPos / 8;
            int inBit = bitPos & 7;
            int bitField = (int)(Mem[curPos + inAddr++] & 0xff);
            bitField |= (int)((Mem[curPos + inAddr++] & 0xff) << 8);
            bitField |= (int)((Mem[curPos + inAddr++] & 0xff) << 16);
            bitField |= (int)((Mem[curPos + inAddr] & 0xff) << 24);
            bitField = Utility.URShift(bitField, inBit);
            return (bitField & (Utility.URShift(unchecked((int)0xffffffff), (32 - bitCount))));
        }


        public virtual void setMemory(int pos, byte[] data, int offset, int dataSize) {
            if (pos < VM_MEMSIZE) {
                //&& data!=Mem+Pos)
                //memmove(Mem+Pos,Data,Min(DataSize,VM_MEMSIZE-Pos));
                for (int i = 0; i < System.Math.Min(data.Length - offset, dataSize); i++) {
                    if ((VM_MEMSIZE - pos) < i) {
                        break;
                    }
                    Mem[pos + i] = data[offset + i];
                }
            }
        }
    }
}

