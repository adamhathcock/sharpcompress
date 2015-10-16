namespace SharpCompress.Compressor.PPMd.I1
{
    using System;

    internal class Allocator
    {
        public uint AllocatorSize;
        public Pointer BaseUnit;
        public uint GlueCount;
        public Pointer Heap;
        private const uint HeapOffset = 0x1d8;
        public Pointer HighUnit;
        private const uint IndexCount = 0x26;
        private static readonly byte[] indexToUnits = new byte[0x26];
        private const uint LocalOffset = 4;
        public Pointer LowUnit;
        public byte[] Memory;
        public MemoryNode[] MemoryNodes = new MemoryNode[0x26];
        private const uint N1 = 4;
        private const uint N2 = 4;
        private const uint N3 = 4;
        private const uint N4 = 0x1a;
        private const uint NodeOffset = 0x10;
        public Pointer Text;
        private const uint UnitSize = 12;
        private static readonly byte[] unitsToIndex;

        static Allocator()
        {
            uint index = 0;
            uint num2 = 1;
            while (index < 4)
            {
                indexToUnits[index] = (byte) num2;
                index++;
                num2++;
            }
            num2++;
            while (index < 8)
            {
                indexToUnits[index] = (byte) num2;
                index++;
                num2 += 2;
            }
            num2++;
            while (index < 12)
            {
                indexToUnits[index] = (byte) num2;
                index++;
                num2 += 3;
            }
            num2++;
            while (index < 0x26)
            {
                indexToUnits[index] = (byte) num2;
                index++;
                num2 += 4;
            }
            unitsToIndex = new byte[0x80];
            for (num2 = index = 0; num2 < 0x80; num2++)
            {
                index += (uint)((indexToUnits[index] < (num2 + 1)) ? 1 : 0);
                unitsToIndex[num2] = (byte) index;
            }
        }

        public Pointer AllocateContext()
        {
            if (this.HighUnit != this.LowUnit)
            {
                return (this.HighUnit -= 12);
            }
            if (this.MemoryNodes[0].Available)
            {
                return this.MemoryNodes[0].Remove();
            }
            return this.AllocateUnitsRare(0);
        }

        public Pointer AllocateUnits(uint unitCount)
        {
            uint index = unitsToIndex[(int) ((IntPtr) (unitCount - 1))];
            if (this.MemoryNodes[index].Available)
            {
                return this.MemoryNodes[index].Remove();
            }
            Pointer lowUnit = this.LowUnit;
            this.LowUnit += indexToUnits[index] * 12;
            if (this.LowUnit <= this.HighUnit)
            {
                return lowUnit;
            }
            this.LowUnit -= indexToUnits[index] * 12;
            return this.AllocateUnitsRare(index);
        }

        private Pointer AllocateUnitsRare(uint index)
        {
            if (this.GlueCount == 0)
            {
                this.GlueFreeBlocks();
                if (this.MemoryNodes[index].Available)
                {
                    return this.MemoryNodes[index].Remove();
                }
            }
            uint num = index;
            do
            {
                if (++num == 0x26)
                {
                    this.GlueCount--;
                    num = (uint) (indexToUnits[index] * 12);
                    return (((this.BaseUnit - this.Text) > num) ? (this.BaseUnit -= num) : Pointer.Zero);
                }
            }
            while (!this.MemoryNodes[num].Available);
            Pointer pointer = this.MemoryNodes[num].Remove();
            this.SplitBlock(pointer, num, index);
            return pointer;
        }

        private void CopyUnits(Pointer target, Pointer source, uint unitCount)
        {
            do
            {
                target[0] = source[0];
                target[1] = source[1];
                target[2] = source[2];
                target[3] = source[3];
                target[4] = source[4];
                target[5] = source[5];
                target[6] = source[6];
                target[7] = source[7];
                target[8] = source[8];
                target[9] = source[9];
                target[10] = source[10];
                target[11] = source[11];
                target += 12;
                source += 12;
            }
            while (--unitCount != 0);
        }

        public void ExpandText()
        {
            MemoryNode node;
            MemoryNode node2;
            uint[] numArray = new uint[0x26];
        Label_0052:
            node2 = node = this.BaseUnit;
            if (node2.Stamp == uint.MaxValue)
            {
                this.BaseUnit = (Pointer) (node + node.UnitCount);
                numArray[unitsToIndex[node.UnitCount - 1]]++;
                node.Stamp = 0;
                goto Label_0052;
            }
            for (uint i = 0; i < 0x26; i++)
            {
                for (node = this.MemoryNodes[i]; numArray[i] != 0; node = node.Next)
                {
                    while (node.Next.Stamp == 0)
                    {
                        node.Unlink();
                        this.MemoryNodes[i].Stamp--;
                        if ((numArray[i] -= 1) == 0)
                        {
                            break;
                        }
                    }
                }
            }
        }

        public Pointer ExpandUnits(Pointer oldPointer, uint oldUnitCount)
        {
            uint index = unitsToIndex[(int) ((IntPtr) (oldUnitCount - 1))];
            uint num2 = unitsToIndex[oldUnitCount];
            if (index == num2)
            {
                return oldPointer;
            }
            Pointer target = this.AllocateUnits(oldUnitCount + 1);
            if (target != Pointer.Zero)
            {
                this.CopyUnits(target, oldPointer, oldUnitCount);
                this.MemoryNodes[index].Insert(oldPointer, oldUnitCount);
            }
            return target;
        }

        public void FreeUnits(Pointer pointer, uint unitCount)
        {
            uint index = unitsToIndex[(int) ((IntPtr) (unitCount - 1))];
            this.MemoryNodes[index].Insert(pointer, indexToUnits[index]);
        }

        public uint GetMemoryUsed()
        {
            uint num = (this.AllocatorSize - (this.HighUnit - this.LowUnit)) - (this.BaseUnit - this.Text);
            for (uint i = 0; i < 0x26; i++)
            {
                num -= (uint) ((12 * indexToUnits[i]) * this.MemoryNodes[i].Stamp);
            }
            return num;
        }
        private void GlueFreeBlocks() {
            MemoryNode memoryNode = new MemoryNode(LocalOffset, Memory);
            memoryNode.Stamp = 0;
            memoryNode.Next = MemoryNode.Zero;
            memoryNode.UnitCount = 0;

            MemoryNode memoryNode0;
            MemoryNode memoryNode1;
            MemoryNode memoryNode2;

            if (LowUnit != HighUnit)
                LowUnit[0] = 0;

            // Find all unused memory nodes.

            memoryNode1 = memoryNode;
            for (uint index = 0; index < IndexCount; index++) {
                while (MemoryNodes[index].Available) {
                    memoryNode0 = MemoryNodes[index].Remove();
                    if (memoryNode0.UnitCount != 0) {
                        while ((memoryNode2 = memoryNode0 + memoryNode0.UnitCount).Stamp == uint.MaxValue) {
                            memoryNode0.UnitCount = memoryNode0.UnitCount + memoryNode2.UnitCount;
                            memoryNode2.UnitCount = 0;
                        }
                        memoryNode1.Link(memoryNode0);
                        memoryNode1 = memoryNode0;
                    }
                }
            }

            // Coalesce the memory represented by the unused memory nodes.

            while (memoryNode.Available) {
                memoryNode0 = memoryNode.Remove();
                uint unitCount = memoryNode0.UnitCount;
                if (unitCount != 0) {
                    for (; unitCount > 128; unitCount -= 128, memoryNode0 += 128)
                        MemoryNodes[IndexCount - 1].Insert(memoryNode0, 128);

                    uint index = unitsToIndex[unitCount - 1];
                    if (indexToUnits[index] != unitCount) {
                        uint unitCountDifference = unitCount - indexToUnits[--index];
                        MemoryNodes[unitCountDifference - 1].Insert(memoryNode0 + (unitCount - unitCountDifference),
                                                                    unitCountDifference);
                    }

                    MemoryNodes[index].Insert(memoryNode0, indexToUnits[index]);
                }
            }

            GlueCount = 1 << 13;
        }
        //private void GlueFreeBlocks()
        //{
        //    MemoryNode node2;
        //    MemoryNode node = new MemoryNode(4, this.Memory);
        //    node.Stamp = 0;
        //    node.Next = MemoryNode.Zero;
        //    node.UnitCount = 0;
        //    if (this.LowUnit != this.HighUnit)
        //    {
        //        this.LowUnit[0] = 0;
        //    }
        //    MemoryNode node3 = node;
        //    uint index = 0;
        //    while (index < 0x26)
        //    {
        //        while (this.MemoryNodes[index].Available)
        //        {
        //            MemoryNode node4;
        //            MemoryNode node5;
        //            node2 = this.MemoryNodes[index].Remove();
        //            if (node2.UnitCount == 0)
        //            {
        //                continue;
        //            }
        //            goto Label_00AE;
        //        Label_008C:
        //            node2.UnitCount += node4.UnitCount;
        //            node4.UnitCount = 0;
        //        Label_00AE:
        //            node5 = node4 = node2 + ((MemoryNode) node2.UnitCount);
        //            if (node5.Stamp == uint.MaxValue)
        //            {
        //                goto Label_008C;
        //            }
        //            node3.Link(node2);
        //            node3 = node2;
        //        }
        //        index++;
        //    }
        //    while (node.Available)
        //    {
        //        node2 = node.Remove();
        //        uint unitCount = node2.UnitCount;
        //        if (unitCount != 0)
        //        {
        //            while (unitCount > 0x80)
        //            {
        //                this.MemoryNodes[0x25].Insert(node2, 0x80);
        //                unitCount -= 0x80;
        //                node2 += 0x80;
        //            }
        //            index = unitsToIndex[(int) ((IntPtr) (unitCount - 1))];
        //            if (indexToUnits[index] != unitCount)
        //            {
        //                uint num3 = unitCount - indexToUnits[(int) ((IntPtr) (--index))];
        //                this.MemoryNodes[(int) ((IntPtr) (num3 - 1))].Insert(node2 + ((MemoryNode) (unitCount - num3)), num3);
        //            }
        //            this.MemoryNodes[index].Insert(node2, indexToUnits[index]);
        //        }
        //    }
        //    this.GlueCount = 0x2000;
        //}

        public void Initialize()
        {
            for (int i = 0; i < IndexCount; i++)
            {
                this.MemoryNodes[i] = new MemoryNode((uint) (0x10L + (i * 12)), this.Memory);
                this.MemoryNodes[i].Stamp = 0;
                this.MemoryNodes[i].Next = MemoryNode.Zero;
                this.MemoryNodes[i].UnitCount = 0;
            }
            this.Text = this.Heap;
            uint num2 = UnitSize * (((this.AllocatorSize / 8) / UnitSize) * 7);
            this.HighUnit = this.Heap + ( this.AllocatorSize);
            this.LowUnit = this.HighUnit - ( num2);
            this.BaseUnit = this.HighUnit - ( num2);
            this.GlueCount = 0;
        }

        public Pointer MoveUnitsUp(Pointer oldPointer, uint unitCount)
        {
            uint index = unitsToIndex[(int) ((IntPtr) (unitCount - 1))];
            if ((oldPointer > (this.BaseUnit + 0x4000)) || (oldPointer > this.MemoryNodes[index].Next))
            {
                return oldPointer;
            }
            Pointer target = this.MemoryNodes[index].Remove();
            this.CopyUnits(target, oldPointer, unitCount);
            unitCount = indexToUnits[index];
            if (oldPointer != this.BaseUnit)
            {
                this.MemoryNodes[index].Insert(oldPointer, unitCount);
            }
            else
            {
                this.BaseUnit += (unitCount * UnitSize);
            }
            return target;
        }

        public Pointer ShrinkUnits(Pointer oldPointer, uint oldUnitCount, uint newUnitCount)
        {
            uint index = unitsToIndex[(int) ((IntPtr) (oldUnitCount - 1))];
            uint num2 = unitsToIndex[(int) ((IntPtr) (newUnitCount - 1))];
            if (index != num2)
            {
                if (this.MemoryNodes[num2].Available)
                {
                    Pointer target = this.MemoryNodes[num2].Remove();
                    this.CopyUnits(target, oldPointer, newUnitCount);
                    this.MemoryNodes[index].Insert(oldPointer, indexToUnits[index]);
                    return target;
                }
                this.SplitBlock(oldPointer, index, num2);
            }
            return oldPointer;
        }

        public void SpecialFreeUnits(Pointer pointer)
        {
            if (pointer != this.BaseUnit)
            {
                this.MemoryNodes[0].Insert(pointer, 1);
            }
            else
            {
                MemoryNode node = pointer;
                node.Stamp = uint.MaxValue;
                this.BaseUnit += 12;
            }
        }

        private void SplitBlock(Pointer pointer, uint oldIndex, uint newIndex)
        {
            uint unitCount = (uint) (indexToUnits[oldIndex] - indexToUnits[newIndex]);
            Pointer memoryNode = pointer + (indexToUnits[newIndex] * 12);
            uint index = unitsToIndex[(int) ((IntPtr) (unitCount - 1))];
            if (indexToUnits[index] != unitCount)
            {
                uint num3 = indexToUnits[(int) ((IntPtr) (--index))];
                this.MemoryNodes[index].Insert(memoryNode, num3);
                memoryNode += (num3 * UnitSize);
                unitCount -= num3;
            }
            this.MemoryNodes[unitsToIndex[(int) ((IntPtr) (unitCount - 1))]].Insert(memoryNode, unitCount);
        }

        public void Start(int allocatorSize)
        {
            uint num = (uint) allocatorSize;
            if (this.AllocatorSize != num)
            {
                this.Stop();
                this.Memory = new byte[0x1d8 + num];
                this.Heap = new Pointer(0x1d8, this.Memory);
                this.AllocatorSize = num;
            }
        }

        public void Stop()
        {
            if (this.AllocatorSize != 0)
            {
                this.AllocatorSize = 0;
                this.Memory = null;
                this.Heap = Pointer.Zero;
            }
        }
    }
}

