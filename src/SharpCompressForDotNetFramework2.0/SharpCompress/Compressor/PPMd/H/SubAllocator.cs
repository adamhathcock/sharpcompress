using System;
using System.Text;
namespace SharpCompress.Compressor.PPMd.H
{
    internal class SubAllocator
    {
        virtual public int FakeUnitsStart
        {
            get
            {
                return fakeUnitsStart;
            }

            set
            {
                this.fakeUnitsStart = value;
            }

        }
        virtual public int HeapEnd
        {
            get
            {
                return heapEnd;
            }

        }
        virtual public int PText
        {
            get
            {
                return pText;
            }

            set
            {
                pText = value;
            }

        }
        virtual public int UnitsStart
        {
            get
            {
                return unitsStart;
            }

            set
            {
                this.unitsStart = value;
            }

        }
        virtual public byte[] Heap
        {
            get
            {
                return heap;
            }

        }
        //UPGRADE_NOTE: Final was removed from the declaration of 'N4 '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public const int N1 = 4;
        public const int N2 = 4;
        public const int N3 = 4;
        public static readonly int N4 = (128 + 3 - 1 * N1 - 2 * N2 - 3 * N3) / 4;

        //UPGRADE_NOTE: Final was removed from the declaration of 'N_INDEXES '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        public static readonly int N_INDEXES = N1 + N2 + N3 + N4;

        //UPGRADE_NOTE: Final was removed from the declaration of 'UNIT_SIZE '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        //UPGRADE_NOTE: The initialization of  'UNIT_SIZE' was moved to static method 'SharpCompress.Unpack.PPM.SubAllocator'. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1005'"
        public static readonly int UNIT_SIZE;

        public const int FIXED_UNIT_SIZE = 12;

        private int subAllocatorSize;

        // byte Indx2Units[N_INDEXES], Units2Indx[128], GlueCount;
        private int[] indx2Units = new int[N_INDEXES];
        private int[] units2Indx = new int[128];
        private int glueCount;

        // byte *HeapStart,*LoUnit, *HiUnit;
        private int heapStart, loUnit, hiUnit;

        //UPGRADE_NOTE: Final was removed from the declaration of 'freeList '. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1003'"
        private RarNode[] freeList = new RarNode[N_INDEXES];

        // byte *pText, *UnitsStart,*HeapEnd,*FakeUnitsStart;
        private int pText, unitsStart, heapEnd, fakeUnitsStart;

        private byte[] heap;

        private int freeListPos;

        private int tempMemBlockPos;

        // Temp fields
        private RarNode tempRarNode = null;
        private RarMemBlock tempRarMemBlock1 = null;
        private RarMemBlock tempRarMemBlock2 = null;
        private RarMemBlock tempRarMemBlock3 = null;

        public SubAllocator()
        {
            clean();
        }

        public virtual void clean()
        {
            subAllocatorSize = 0;
        }

        private void insertNode(int p, int indx)
        {
            RarNode temp = tempRarNode;
            temp.Address = p;
            temp.SetNext(freeList[indx].GetNext());
            freeList[indx].SetNext(temp);
        }

        public virtual void incPText()
        {
            pText++;
        }

        private int removeNode(int indx)
        {
            int retVal = freeList[indx].GetNext();
            RarNode temp = tempRarNode;
            temp.Address = retVal;
            freeList[indx].SetNext(temp.GetNext());
            return retVal;
        }

        private int U2B(int NU)
        {
            return UNIT_SIZE * NU;
        }

        /* memblockptr */
        private int MBPtr(int BasePtr, int Items)
        {
            return (BasePtr + U2B(Items));
        }

        private void splitBlock(int pv, int oldIndx, int newIndx)
        {
            int i, uDiff = indx2Units[oldIndx] - indx2Units[newIndx];
            int p = pv + U2B(indx2Units[newIndx]);
            if (indx2Units[i = units2Indx[uDiff - 1]] != uDiff)
            {
                insertNode(p, --i);
                p += U2B(i = indx2Units[i]);
                uDiff -= i;
            }
            insertNode(p, units2Indx[uDiff - 1]);
        }

        public virtual void stopSubAllocator()
        {
            if (subAllocatorSize != 0)
            {
                subAllocatorSize = 0;
                //ArrayFactory.BYTES_FACTORY.recycle(heap);
                heap = null;
                heapStart = 1;
                // rarfree(HeapStart);
                // Free temp fields
                tempRarNode = null;
                tempRarMemBlock1 = null;
                tempRarMemBlock2 = null;
                tempRarMemBlock3 = null;
            }
        }

        public virtual int GetAllocatedMemory()
        {
            return subAllocatorSize;
        }


        public virtual bool startSubAllocator(int SASize)
        {
            int t = SASize;
            if (subAllocatorSize == t)
            {
                return true;
            }
            stopSubAllocator();
            int allocSize = t / FIXED_UNIT_SIZE * UNIT_SIZE + UNIT_SIZE;

            // adding space for freelist (needed for poiters)
            // 1+ for null pointer
            int realAllocSize = 1 + allocSize + 4 * N_INDEXES;
            // adding space for an additional memblock
            tempMemBlockPos = realAllocSize;
            realAllocSize += RarMemBlock.size;

            heap = new byte[realAllocSize];
            heapStart = 1;
            heapEnd = heapStart + allocSize - UNIT_SIZE;
            subAllocatorSize = t;
            // Bug fixed
            freeListPos = heapStart + allocSize;
            //UPGRADE_ISSUE: The following fragment of code could not be parsed and was not converted. "ms-help://MS.VSCC.v80/dv_commoner/local/redirect.htm?index='!DefaultContextWindowIndex'&keyword='jlca1156'"
            //assert(realAllocSize - tempMemBlockPos == RarMemBlock.size): realAllocSize 
            //+   + tempMemBlockPos +   + RarMemBlock.size;

            // Init freeList
            for (int i = 0, pos = freeListPos; i < freeList.Length; i++, pos += RarNode.size)
            {
                freeList[i] = new RarNode(heap);
                freeList[i].Address = pos;
            }

            // Init temp fields
            tempRarNode = new RarNode(heap);
            tempRarMemBlock1 = new RarMemBlock(heap);
            tempRarMemBlock2 = new RarMemBlock(heap);
            tempRarMemBlock3 = new RarMemBlock(heap);

            return true;
        }

        private void glueFreeBlocks()
        {
            RarMemBlock s0 = tempRarMemBlock1;
            s0.Address = tempMemBlockPos;
            RarMemBlock p = tempRarMemBlock2;
            RarMemBlock p1 = tempRarMemBlock3;
            int i, k, sz;
            if (loUnit != hiUnit)
            {
                heap[loUnit] = 0;
            }
            for (i = 0, s0.SetPrev(s0), s0.SetNext(s0); i < N_INDEXES; i++)
            {
                while (freeList[i].GetNext() != 0)
                {
                    p.Address = removeNode(i); // =(RAR_MEM_BLK*)RemoveNode(i);
                    p.InsertAt(s0); // p->insertAt(&s0);
                    p.Stamp = 0xFFFF; // p->Stamp=0xFFFF;
                    p.SetNU(indx2Units[i]); // p->NU=Indx2Units[i];
                }
            }
            for (p.Address = s0.GetNext(); p.Address != s0.Address; p.Address = p.GetNext())
            {
                // while ((p1=MBPtr(p,p->NU))->Stamp == 0xFFFF && int(p->NU)+p1->NU
                // < 0x10000)
                // Bug fixed
                p1.Address = MBPtr(p.Address, p.GetNU());
                while (p1.Stamp == 0xFFFF && p.GetNU() + p1.GetNU() < 0x10000)
                {
                    p1.Remove();
                    p.SetNU(p.GetNU() + p1.GetNU()); // ->NU += p1->NU;
                    p1.Address = MBPtr(p.Address, p.GetNU());
                }
            }
            // while ((p=s0.next) != &s0)
            // Bug fixed
            p.Address = s0.GetNext();
            while (p.Address != s0.Address)
            {
                for (p.Remove(), sz = p.GetNU(); sz > 128; sz -= 128, p.Address = MBPtr(p.Address, 128))
                {
                    insertNode(p.Address, N_INDEXES - 1);
                }
                if (indx2Units[i = units2Indx[sz - 1]] != sz)
                {
                    k = sz - indx2Units[--i];
                    insertNode(MBPtr(p.Address, sz - k), k - 1);
                }
                insertNode(p.Address, i);
                p.Address = s0.GetNext();
            }
        }

        private int allocUnitsRare(int indx)
        {
            if (glueCount == 0)
            {
                glueCount = 255;
                glueFreeBlocks();
                if (freeList[indx].GetNext() != 0)
                {
                    return removeNode(indx);
                }
            }
            int i = indx;
            do
            {
                if (++i == N_INDEXES)
                {
                    glueCount--;
                    i = U2B(indx2Units[indx]);
                    int j = FIXED_UNIT_SIZE * indx2Units[indx];
                    if (fakeUnitsStart - pText > j)
                    {
                        fakeUnitsStart -= j;
                        unitsStart -= i;
                        return unitsStart;
                    }
                    return (0);
                }
            }
            while (freeList[i].GetNext() == 0);
            int retVal = removeNode(i);
            splitBlock(retVal, i, indx);
            return retVal;
        }

        public virtual int allocUnits(int NU)
        {
            int indx = units2Indx[NU - 1];
            if (freeList[indx].GetNext() != 0)
            {
                return removeNode(indx);
            }
            int retVal = loUnit;
            loUnit += U2B(indx2Units[indx]);
            if (loUnit <= hiUnit)
            {
                return retVal;
            }
            loUnit -= U2B(indx2Units[indx]);
            return allocUnitsRare(indx);
        }

        public virtual int allocContext()
        {
            if (hiUnit != loUnit)
                return (hiUnit -= UNIT_SIZE);
            if (freeList[0].GetNext() != 0)
            {
                return removeNode(0);
            }
            return allocUnitsRare(0);
        }

        public virtual int expandUnits(int oldPtr, int OldNU)
        {
            int i0 = units2Indx[OldNU - 1];
            int i1 = units2Indx[OldNU - 1 + 1];
            if (i0 == i1)
            {
                return oldPtr;
            }
            int ptr = allocUnits(OldNU + 1);
            if (ptr != 0)
            {
                // memcpy(ptr,OldPtr,U2B(OldNU));
                Array.Copy(heap, oldPtr, heap, ptr, U2B(OldNU));
                insertNode(oldPtr, i0);
            }
            return ptr;
        }

        public virtual int shrinkUnits(int oldPtr, int oldNU, int newNU)
        {
            // System.out.println("SubAllocator.shrinkUnits(" + OldPtr + ", " +
            // OldNU + ", " + NewNU + ")");
            int i0 = units2Indx[oldNU - 1];
            int i1 = units2Indx[newNU - 1];
            if (i0 == i1)
            {
                return oldPtr;
            }
            if (freeList[i1].GetNext() != 0)
            {
                int ptr = removeNode(i1);
                // memcpy(ptr,OldPtr,U2B(NewNU));
                // for (int i = 0; i < U2B(NewNU); i++) {
                // heap[ptr + i] = heap[OldPtr + i];
                // }
                Array.Copy(heap, oldPtr, heap, ptr, U2B(newNU));
                insertNode(oldPtr, i0);
                return ptr;
            }
            else
            {
                splitBlock(oldPtr, i0, i1);
                return oldPtr;
            }
        }

        public virtual void freeUnits(int ptr, int OldNU)
        {
            insertNode(ptr, units2Indx[OldNU - 1]);
        }

        public virtual void decPText(int dPText)
        {
            PText = PText - dPText;
        }

        public virtual void initSubAllocator()
        {
            int i, k;
            Utility.Fill(heap, freeListPos, freeListPos + sizeOfFreeList(), (byte)0);

            pText = heapStart;

            int size2 = FIXED_UNIT_SIZE * (subAllocatorSize / 8 / FIXED_UNIT_SIZE * 7);
            int realSize2 = size2 / FIXED_UNIT_SIZE * UNIT_SIZE;
            int size1 = subAllocatorSize - size2;
            int realSize1 = size1 / FIXED_UNIT_SIZE * UNIT_SIZE + size1 % FIXED_UNIT_SIZE;
            hiUnit = heapStart + subAllocatorSize;
            loUnit = unitsStart = heapStart + realSize1;
            fakeUnitsStart = heapStart + size1;
            hiUnit = loUnit + realSize2;

            for (i = 0, k = 1; i < N1; i++, k += 1)
            {
                indx2Units[i] = k & 0xff;
            }
            for (k++; i < N1 + N2; i++, k += 2)
            {
                indx2Units[i] = k & 0xff;
            }
            for (k++; i < N1 + N2 + N3; i++, k += 3)
            {
                indx2Units[i] = k & 0xff;
            }
            for (k++; i < (N1 + N2 + N3 + N4); i++, k += 4)
            {
                indx2Units[i] = k & 0xff;
            }

            for (glueCount = 0, k = 0, i = 0; k < 128; k++)
            {
                i += ((indx2Units[i] < (k + 1)) ? 1 : 0);
                units2Indx[k] = i & 0xff;
            }
        }

        private int sizeOfFreeList()
        {
            return freeList.Length * RarNode.size;
        }

        // Debug
        // public void dumpHeap() {
        // File file = new File("P:\\test\\heapdumpj");
        // OutputStream out = null;
        // try {
        // out = new FileOutputStream(file);
        // out.write(heap, heapStart, heapEnd - heapStart);
        // out.flush();
        // System.out.println("Heap dumped to " + file.getAbsolutePath());
        // }
        // catch (IOException e) {
        // e.printStackTrace();
        // }
        // finally {
        // FileUtil.close(out);
        // }
        // }

        // Debug
        public override System.String ToString()
        {
            StringBuilder buffer = new StringBuilder();
            buffer.Append("SubAllocator[");
            buffer.Append("\n  subAllocatorSize=");
            buffer.Append(subAllocatorSize);
            buffer.Append("\n  glueCount=");
            buffer.Append(glueCount);
            buffer.Append("\n  heapStart=");
            buffer.Append(heapStart);
            buffer.Append("\n  loUnit=");
            buffer.Append(loUnit);
            buffer.Append("\n  hiUnit=");
            buffer.Append(hiUnit);
            buffer.Append("\n  pText=");
            buffer.Append(pText);
            buffer.Append("\n  unitsStart=");
            buffer.Append(unitsStart);
            buffer.Append("\n]");
            return buffer.ToString();
        }
        static SubAllocator()
        {
            UNIT_SIZE = System.Math.Max(PPMContext.size, RarMemBlock.size);
        }
    }
}