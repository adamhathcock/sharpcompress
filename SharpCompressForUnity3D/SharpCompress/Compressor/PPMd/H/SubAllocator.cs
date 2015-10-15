namespace SharpCompress.Compressor.PPMd.H
{
    using SharpCompress;
    using System;
    using System.Text;

    internal class SubAllocator
    {
        private int fakeUnitsStart;
        public const int FIXED_UNIT_SIZE = 12;
        private RarNode[] freeList = new RarNode[N_INDEXES];
        private int freeListPos;
        private int glueCount;
        private byte[] heap;
        private int heapEnd;
        private int heapStart;
        private int hiUnit;
        private int[] indx2Units = new int[N_INDEXES];
        private int loUnit;
        public static readonly int N_INDEXES = (12 + N4);
        public const int N1 = 4;
        public const int N2 = 4;
        public const int N3 = 4;
        public static readonly int N4 = 0x1a;
        private int pText;
        private int subAllocatorSize;
        private int tempMemBlockPos;
        private RarMemBlock tempRarMemBlock1 = null;
        private RarMemBlock tempRarMemBlock2 = null;
        private RarMemBlock tempRarMemBlock3 = null;
        private RarNode tempRarNode = null;
        public static readonly int UNIT_SIZE = Math.Max(PPMContext.size, 12);
        private int[] units2Indx = new int[0x80];
        private int unitsStart;

        public SubAllocator()
        {
            this.clean();
        }

        public virtual int allocContext()
        {
            if (this.hiUnit != this.loUnit)
            {
                return (this.hiUnit -= UNIT_SIZE);
            }
            if (this.freeList[0].GetNext() != 0)
            {
                return this.removeNode(0);
            }
            return this.allocUnitsRare(0);
        }

        public virtual int allocUnits(int NU)
        {
            int index = this.units2Indx[NU - 1];
            if (this.freeList[index].GetNext() != 0)
            {
                return this.removeNode(index);
            }
            int loUnit = this.loUnit;
            this.loUnit += this.U2B(this.indx2Units[index]);
            if (this.loUnit <= this.hiUnit)
            {
                return loUnit;
            }
            this.loUnit -= this.U2B(this.indx2Units[index]);
            return this.allocUnitsRare(index);
        }

        private int allocUnitsRare(int indx)
        {
            if (this.glueCount == 0)
            {
                this.glueCount = 0xff;
                this.glueFreeBlocks();
                if (this.freeList[indx].GetNext() != 0)
                {
                    return this.removeNode(indx);
                }
            }
            int index = indx;
            do
            {
                if (++index == N_INDEXES)
                {
                    this.glueCount--;
                    index = this.U2B(this.indx2Units[indx]);
                    int num2 = 12 * this.indx2Units[indx];
                    if ((this.fakeUnitsStart - this.pText) > num2)
                    {
                        this.fakeUnitsStart -= num2;
                        this.unitsStart -= index;
                        return this.unitsStart;
                    }
                    return 0;
                }
            }
            while (this.freeList[index].GetNext() == 0);
            int pv = this.removeNode(index);
            this.splitBlock(pv, index, indx);
            return pv;
        }

        public virtual void clean()
        {
            this.subAllocatorSize = 0;
        }

        public virtual void decPText(int dPText)
        {
            this.PText -= dPText;
        }

        public virtual int expandUnits(int oldPtr, int OldNU)
        {
            int indx = this.units2Indx[OldNU - 1];
            int num2 = this.units2Indx[(OldNU - 1) + 1];
            if (indx == num2)
            {
                return oldPtr;
            }
            int destinationIndex = this.allocUnits(OldNU + 1);
            if (destinationIndex != 0)
            {
                Array.Copy(this.heap, oldPtr, this.heap, destinationIndex, this.U2B(OldNU));
                this.insertNode(oldPtr, indx);
            }
            return destinationIndex;
        }

        public virtual void freeUnits(int ptr, int OldNU)
        {
            this.insertNode(ptr, this.units2Indx[OldNU - 1]);
        }

        public virtual int GetAllocatedMemory()
        {
            return this.subAllocatorSize;
        }

        private void glueFreeBlocks()
        {
            RarMemBlock prev = this.tempRarMemBlock1;
            prev.Address = this.tempMemBlockPos;
            RarMemBlock block2 = this.tempRarMemBlock2;
            RarMemBlock block3 = this.tempRarMemBlock3;
            if (this.loUnit != this.hiUnit)
            {
                this.heap[this.loUnit] = 0;
            }
            int index = 0;
            prev.SetPrev(prev);
            prev.SetNext(prev);
            while (index < N_INDEXES)
            {
                while (this.freeList[index].GetNext() != 0)
                {
                    block2.Address = this.removeNode(index);
                    block2.InsertAt(prev);
                    block2.Stamp = 0xffff;
                    block2.SetNU(this.indx2Units[index]);
                }
                index++;
            }
            block2.Address = prev.GetNext();
            while (block2.Address != prev.Address)
            {
                block3.Address = this.MBPtr(block2.Address, block2.GetNU());
                while ((block3.Stamp == 0xffff) && ((block2.GetNU() + block3.GetNU()) < 0x10000))
                {
                    block3.Remove();
                    block2.SetNU(block2.GetNU() + block3.GetNU());
                    block3.Address = this.MBPtr(block2.Address, block2.GetNU());
                }
                block2.Address = block2.GetNext();
            }
            block2.Address = prev.GetNext();
            while (block2.Address != prev.Address)
            {
                block2.Remove();
                int nU = block2.GetNU();
                while (nU > 0x80)
                {
                    this.insertNode(block2.Address, N_INDEXES - 1);
                    nU -= 0x80;
                    block2.Address = this.MBPtr(block2.Address, 0x80);
                }
                if (this.indx2Units[index = this.units2Indx[nU - 1]] != nU)
                {
                    int num2 = nU - this.indx2Units[--index];
                    this.insertNode(this.MBPtr(block2.Address, nU - num2), num2 - 1);
                }
                this.insertNode(block2.Address, index);
                block2.Address = prev.GetNext();
            }
        }

        public virtual void incPText()
        {
            this.pText++;
        }

        public virtual void initSubAllocator()
        {
            Utility.Fill<byte>(this.heap, this.freeListPos, this.freeListPos + this.sizeOfFreeList(), 0);
            this.pText = this.heapStart;
            int num3 = 12 * (((this.subAllocatorSize / 8) / 12) * 7);
            int num4 = (num3 / 12) * UNIT_SIZE;
            int num5 = this.subAllocatorSize - num3;
            int num6 = ((num5 / 12) * UNIT_SIZE) + (num5 % 12);
            this.hiUnit = this.heapStart + this.subAllocatorSize;
            this.loUnit = this.unitsStart = this.heapStart + num6;
            this.fakeUnitsStart = this.heapStart + num5;
            this.hiUnit = this.loUnit + num4;
            int index = 0;
            int num2 = 1;
            while (index < 4)
            {
                this.indx2Units[index] = num2 & 0xff;
                index++;
                num2++;
            }
            num2++;
            while (index < 8)
            {
                this.indx2Units[index] = num2 & 0xff;
                index++;
                num2 += 2;
            }
            num2++;
            while (index < 12)
            {
                this.indx2Units[index] = num2 & 0xff;
                index++;
                num2 += 3;
            }
            num2++;
            while (index < (12 + N4))
            {
                this.indx2Units[index] = num2 & 0xff;
                index++;
                num2 += 4;
            }
            this.glueCount = 0;
            num2 = 0;
            index = 0;
            while (num2 < 0x80)
            {
                index += (this.indx2Units[index] < (num2 + 1)) ? 1 : 0;
                this.units2Indx[num2] = index & 0xff;
                num2++;
            }
        }

        private void insertNode(int p, int indx)
        {
            RarNode tempRarNode = this.tempRarNode;
            tempRarNode.Address = p;
            tempRarNode.SetNext(this.freeList[indx].GetNext());
            this.freeList[indx].SetNext(tempRarNode);
        }

        private int MBPtr(int BasePtr, int Items)
        {
            return (BasePtr + this.U2B(Items));
        }

        private int removeNode(int indx)
        {
            int next = this.freeList[indx].GetNext();
            RarNode tempRarNode = this.tempRarNode;
            tempRarNode.Address = next;
            this.freeList[indx].SetNext(tempRarNode.GetNext());
            return next;
        }

        public virtual int shrinkUnits(int oldPtr, int oldNU, int newNU)
        {
            int indx = this.units2Indx[oldNU - 1];
            int index = this.units2Indx[newNU - 1];
            if (indx != index)
            {
                if (this.freeList[index].GetNext() != 0)
                {
                    int destinationIndex = this.removeNode(index);
                    Array.Copy(this.heap, oldPtr, this.heap, destinationIndex, this.U2B(newNU));
                    this.insertNode(oldPtr, indx);
                    return destinationIndex;
                }
                this.splitBlock(oldPtr, indx, index);
            }
            return oldPtr;
        }

        private int sizeOfFreeList()
        {
            return (this.freeList.Length * 4);
        }

        private void splitBlock(int pv, int oldIndx, int newIndx)
        {
            int num;
            int num2 = this.indx2Units[oldIndx] - this.indx2Units[newIndx];
            int p = pv + this.U2B(this.indx2Units[newIndx]);
            if (this.indx2Units[num = this.units2Indx[num2 - 1]] != num2)
            {
                this.insertNode(p, --num);
                p += this.U2B(num = this.indx2Units[num]);
                num2 -= num;
            }
            this.insertNode(p, this.units2Indx[num2 - 1]);
        }

        public virtual bool startSubAllocator(int SASize)
        {
            int num = SASize;
            if (this.subAllocatorSize != num)
            {
                this.stopSubAllocator();
                int num2 = ((num / 12) * UNIT_SIZE) + UNIT_SIZE;
                int num3 = (1 + num2) + (4 * N_INDEXES);
                this.tempMemBlockPos = num3;
                num3 += 12;
                this.heap = new byte[num3];
                this.heapStart = 1;
                this.heapEnd = (this.heapStart + num2) - UNIT_SIZE;
                this.subAllocatorSize = num;
                this.freeListPos = this.heapStart + num2;
                int index = 0;
                for (int i = this.freeListPos; index < this.freeList.Length; i += 4)
                {
                    this.freeList[index] = new RarNode(this.heap);
                    this.freeList[index].Address = i;
                    index++;
                }
                this.tempRarNode = new RarNode(this.heap);
                this.tempRarMemBlock1 = new RarMemBlock(this.heap);
                this.tempRarMemBlock2 = new RarMemBlock(this.heap);
                this.tempRarMemBlock3 = new RarMemBlock(this.heap);
            }
            return true;
        }

        public virtual void stopSubAllocator()
        {
            if (this.subAllocatorSize != 0)
            {
                this.subAllocatorSize = 0;
                this.heap = null;
                this.heapStart = 1;
                this.tempRarNode = null;
                this.tempRarMemBlock1 = null;
                this.tempRarMemBlock2 = null;
                this.tempRarMemBlock3 = null;
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("SubAllocator[");
            builder.Append("\n  subAllocatorSize=");
            builder.Append(this.subAllocatorSize);
            builder.Append("\n  glueCount=");
            builder.Append(this.glueCount);
            builder.Append("\n  heapStart=");
            builder.Append(this.heapStart);
            builder.Append("\n  loUnit=");
            builder.Append(this.loUnit);
            builder.Append("\n  hiUnit=");
            builder.Append(this.hiUnit);
            builder.Append("\n  pText=");
            builder.Append(this.pText);
            builder.Append("\n  unitsStart=");
            builder.Append(this.unitsStart);
            builder.Append("\n]");
            return builder.ToString();
        }

        private int U2B(int NU)
        {
            return (UNIT_SIZE * NU);
        }

        public virtual int FakeUnitsStart
        {
            get
            {
                return this.fakeUnitsStart;
            }
            set
            {
                this.fakeUnitsStart = value;
            }
        }

        public virtual byte[] Heap
        {
            get
            {
                return this.heap;
            }
        }

        public virtual int HeapEnd
        {
            get
            {
                return this.heapEnd;
            }
        }

        public virtual int PText
        {
            get
            {
                return this.pText;
            }
            set
            {
                this.pText = value;
            }
        }

        public virtual int UnitsStart
        {
            get
            {
                return this.unitsStart;
            }
            set
            {
                this.unitsStart = value;
            }
        }
    }
}

