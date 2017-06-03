#region Using



#endregion

namespace SharpCompress.Compressor.PPMd.I1
{

    /// Allocate a single, large array and then provide sections of this array to callers.  Callers are provided with
    /// instances of <see cref="Pointer"/> (which simply contain a single address value, representing a location
    /// in the large array).  Callers can then cast <see cref="Pointer"/> to one of the following structures (all
    /// of which also simply contain a single address value):
    internal class Allocator
    {
        private const uint UnitSize = 12;
        private const uint LocalOffset = 4;  // reserve the first four bytes for Pointer.Zero
        private const uint NodeOffset = LocalOffset + MemoryNode.Size;  // reserve space for a single memory node
        private const uint HeapOffset = NodeOffset + IndexCount * MemoryNode.Size;  // reserve space for the array of memory nodes
        private const uint N1 = 4;
        private const uint N2 = 4;
        private const uint N3 = 4;
        private const uint N4 = (128 + 3 - 1 * N1 - 2 * N2 - 3 * N3) / 4;
        private const uint IndexCount = N1 + N2 + N3 + N4;

        private static readonly byte[] indexToUnits;
        private static readonly byte[] unitsToIndex;

        public uint AllocatorSize;
        public uint GlueCount;
        public Pointer BaseUnit;
        public Pointer LowUnit;
        public Pointer HighUnit;
        public Pointer Text;
        public Pointer Heap;
        public MemoryNode[] MemoryNodes;

        public byte[] Memory;

        /// <summary>
        /// Initializes static read-only arrays used by the <see cref="Allocator"/>.
        /// </summary>
        static Allocator()
        {
            // Construct the static index to units lookup array.  It will contain the following values.
            //
            // 1 2 3 4 6 8 10 12 15 18 21 24 28 32 36 40 44 48 52 56 60 64 68 72 76 80 84 88 92 96 100 104 108
            // 112 116 120 124 128

            uint index;
            uint unitCount;

            indexToUnits = new byte[IndexCount];

            for (index = 0, unitCount = 1; index < N1; index++, unitCount += 1)
                indexToUnits[index] = (byte)unitCount;

            for (unitCount++; index < N1 + N2; index++, unitCount += 2)
                indexToUnits[index] = (byte)unitCount;

            for (unitCount++; index < N1 + N2 + N3; index++, unitCount += 3)
                indexToUnits[index] = (byte)unitCount;

            for (unitCount++; index < N1 + N2 + N3 + N4; index++, unitCount += 4)
                indexToUnits[index] = (byte)unitCount;

            // Construct the static units to index lookup array.  It will contain the following values.
            //
            // 00 01 02 03 04 04 05 05 06 06 07 07 08 08 08 09 09 09 10 10 10 11 11 11 12 12 12 12 13 13 13 13
            // 14 14 14 14 15 15 15 15 16 16 16 16 17 17 17 17 18 18 18 18 19 19 19 19 20 20 20 20 21 21 21 21
            // 22 22 22 22 23 23 23 23 24 24 24 24 25 25 25 25 26 26 26 26 27 27 27 27 28 28 28 28 29 29 29 29
            // 30 30 30 30 31 31 31 31 32 32 32 32 33 33 33 33 34 34 34 34 35 35 35 35 36 36 36 36 37 37 37 37

            unitsToIndex = new byte[128];

            for (unitCount = index = 0; unitCount < 128; unitCount++)
            {
                index += (uint)((indexToUnits[index] < unitCount + 1) ? 1 : 0);
                unitsToIndex[unitCount] = (byte)index;
            }
        }

        #region Public Methods

        public Allocator()
        {
            MemoryNodes = new MemoryNode[IndexCount];
        }

        /// <summary>
        /// Initialize or reset the memory allocator (so that the single, large array can be re-used without destroying
        /// and re-creating it).
        /// </summary>
        public void Initialize()
        {
            for (int index = 0; index < IndexCount; index++)
            {
                MemoryNodes[index] = new MemoryNode((uint)(NodeOffset + index * MemoryNode.Size), Memory);
                MemoryNodes[index].Stamp = 0;
                MemoryNodes[index].Next = MemoryNode.Zero;
                MemoryNodes[index].UnitCount = 0;
            }

            Text = Heap;

            uint difference = UnitSize * (AllocatorSize / 8 / UnitSize * 7);

            HighUnit = Heap + AllocatorSize;
            LowUnit = HighUnit - difference;
            BaseUnit = HighUnit - difference;

            GlueCount = 0;
        }

        /// <summary>
        /// Start the allocator (create a single, large array of bytes).
        /// </summary>
        /// <remarks>
        /// Note that .NET will create that array on the large object heap (because it is so large).
        /// </remarks>
        /// <param name="allocatorSize"></param>
        public void Start(int allocatorSize)
        {
            uint size = (uint)allocatorSize;
            if (AllocatorSize != size)
            {
                Stop();
                Memory = new byte[HeapOffset + size];  // the single, large array of bytes
                Heap = new Pointer(HeapOffset, Memory);  // reserve bytes in the range 0 .. HeapOffset - 1
                AllocatorSize = size;
            }
        }

        /// <summary>
        /// Stop the allocator (free the single, large array of bytes).  This can safely be called multiple times (without
        /// intervening calls to <see cref="Start"/>).
        /// </summary>
        /// <remarks>
        /// Because the array is on the large object heap it may not be freed immediately.
        /// </remarks>
        public void Stop()
        {
            if (AllocatorSize != 0)
            {
                AllocatorSize = 0;
                Memory = null;
                Heap = Pointer.Zero;
            }
        }

        /// <summary>
        /// Determine how much memory (from the single, large array) is currenly in use.
        /// </summary>
        /// <returns></returns>
        public uint GetMemoryUsed()
        {
            uint memoryUsed = AllocatorSize - (HighUnit - LowUnit) - (BaseUnit - Text);
            for (uint index = 0; index < IndexCount; index++)
                memoryUsed -= UnitSize * indexToUnits[index] * MemoryNodes[index].Stamp;
            return memoryUsed;
        }

        /// <summary>
        /// Allocate a given number of units from the single, large array.  Each unit is <see cref="UnitSize"/> bytes
        /// in size.
        /// </summary>
        /// <param name="unitCount"></param>
        /// <returns></returns>
        public Pointer AllocateUnits(uint unitCount)
        {
            uint index = unitsToIndex[unitCount - 1];
            if (MemoryNodes[index].Available)
                return MemoryNodes[index].Remove();

            Pointer allocatedBlock = LowUnit;
            LowUnit += indexToUnits[index] * UnitSize;
            if (LowUnit <= HighUnit)
                return allocatedBlock;

            LowUnit -= indexToUnits[index] * UnitSize;
            return AllocateUnitsRare(index);
        }

        /// <summary>
        /// Allocate enough space for a PpmContext instance in the single, large array.
        /// </summary>
        /// <returns></returns>
        public Pointer AllocateContext()
        {
            if (HighUnit != LowUnit)
                return (HighUnit -= UnitSize);
            else if (MemoryNodes[0].Available)
                return MemoryNodes[0].Remove();
            else
                return AllocateUnitsRare(0);
        }

        /// <summary>
        /// Increase the size of an existing allocation (represented by a <see cref="Pointer"/>).
        /// </summary>
        /// <param name="oldPointer"></param>
        /// <param name="oldUnitCount"></param>
        /// <returns></returns>
        public Pointer ExpandUnits(Pointer oldPointer, uint oldUnitCount)
        {
            uint oldIndex = unitsToIndex[oldUnitCount - 1];
            uint newIndex = unitsToIndex[oldUnitCount];

            if (oldIndex == newIndex)
                return oldPointer;

            Pointer pointer = AllocateUnits(oldUnitCount + 1);

            if (pointer != Pointer.Zero)
            {
                CopyUnits(pointer, oldPointer, oldUnitCount);
                MemoryNodes[oldIndex].Insert(oldPointer, oldUnitCount);
            }

            return pointer;
        }

        /// <summary>
        /// Decrease the size of an existing allocation (represented by a <see cref="Pointer"/>).
        /// </summary>
        /// <param name="oldPointer"></param>
        /// <param name="oldUnitCount"></param>
        /// <param name="newUnitCount"></param>
        /// <returns></returns>
        public Pointer ShrinkUnits(Pointer oldPointer, uint oldUnitCount, uint newUnitCount)
        {
            uint oldIndex = unitsToIndex[oldUnitCount - 1];
            uint newIndex = unitsToIndex[newUnitCount - 1];

            if (oldIndex == newIndex)
                return oldPointer;

            if (MemoryNodes[newIndex].Available)
            {
                Pointer pointer = MemoryNodes[newIndex].Remove();
                CopyUnits(pointer, oldPointer, newUnitCount);
                MemoryNodes[oldIndex].Insert(oldPointer, indexToUnits[oldIndex]);
                return pointer;
            }
            else
            {
                SplitBlock(oldPointer, oldIndex, newIndex);
                return oldPointer;
            }
        }

        /// <summary>
        /// Free previously allocated space (the location and amount of space to free must be specified by using
        /// a <see cref="Pointer"/> to indicate the location and a number of units to indicate the amount).
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="unitCount"></param>
        public void FreeUnits(Pointer pointer, uint unitCount)
        {
            uint index = unitsToIndex[unitCount - 1];
            MemoryNodes[index].Insert(pointer, indexToUnits[index]);
        }

        public void SpecialFreeUnits(Pointer pointer)
        {
            if (pointer != BaseUnit)
            {
                MemoryNodes[0].Insert(pointer, 1);
            }
            else
            {
                MemoryNode memoryNode = pointer;
                memoryNode.Stamp = uint.MaxValue;
                BaseUnit += UnitSize;
            }
        }

        public Pointer MoveUnitsUp(Pointer oldPointer, uint unitCount)
        {
            uint index = unitsToIndex[unitCount - 1];

            if (oldPointer > BaseUnit + 16 * 1024 || oldPointer > MemoryNodes[index].Next)
                return oldPointer;

            Pointer pointer = MemoryNodes[index].Remove();
            CopyUnits(pointer, oldPointer, unitCount);
            unitCount = indexToUnits[index];

            if (oldPointer != BaseUnit)
                MemoryNodes[index].Insert(oldPointer, unitCount);
            else
                BaseUnit += unitCount * UnitSize;

            return pointer;
        }

        /// <summary>
        /// Expand the space allocated (in the single, large array) for the bytes of the data (ie. the "text") that is
        /// being encoded or decoded.
        /// </summary>
        public void ExpandText()
        {
            MemoryNode memoryNode;
            uint[] counts = new uint[IndexCount];

            while ((memoryNode = BaseUnit).Stamp == uint.MaxValue)
            {
                BaseUnit = memoryNode + memoryNode.UnitCount;
                counts[unitsToIndex[memoryNode.UnitCount - 1]]++;
                memoryNode.Stamp = 0;
            }

            for (uint index = 0; index < IndexCount; index++)
            {
                for (memoryNode = MemoryNodes[index]; counts[index] != 0; memoryNode = memoryNode.Next)
                {
                    while (memoryNode.Next.Stamp == 0)
                    {
                        memoryNode.Unlink();
                        MemoryNodes[index].Stamp--;
                        if (--counts[index] == 0)
                            break;
                    }
                }
            }
        }

        #endregion

        #region Private Methods

        private Pointer AllocateUnitsRare(uint index)
        {
            if (GlueCount == 0)
            {
                GlueFreeBlocks();
                if (MemoryNodes[index].Available)
                    return MemoryNodes[index].Remove();
            }

            uint oldIndex = index;
            do
            {
                if (++oldIndex == IndexCount)
                {
                    GlueCount--;
                    oldIndex = indexToUnits[index] * UnitSize;
                    return (BaseUnit - Text > oldIndex) ? (BaseUnit -= oldIndex) : Pointer.Zero;
                }
            } while (!MemoryNodes[oldIndex].Available);

            Pointer allocatedBlock = MemoryNodes[oldIndex].Remove();
            SplitBlock(allocatedBlock, oldIndex, index);
            return allocatedBlock;
        }

        private void SplitBlock(Pointer pointer, uint oldIndex, uint newIndex)
        {
            uint unitCountDifference = (uint)(indexToUnits[oldIndex] - indexToUnits[newIndex]);
            Pointer newPointer = pointer + indexToUnits[newIndex] * UnitSize;

            uint index = unitsToIndex[unitCountDifference - 1];
            if (indexToUnits[index] != unitCountDifference)
            {
                uint unitCount = indexToUnits[--index];
                MemoryNodes[index].Insert(newPointer, unitCount);
                newPointer += unitCount * UnitSize;
                unitCountDifference -= unitCount;
            }

            MemoryNodes[unitsToIndex[unitCountDifference - 1]].Insert(newPointer, unitCountDifference);
        }

        private void GlueFreeBlocks()
        {
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
            for (uint index = 0; index < IndexCount; index++)
            {
                while (MemoryNodes[index].Available)
                {
                    memoryNode0 = MemoryNodes[index].Remove();
                    if (memoryNode0.UnitCount != 0)
                    {
                        while ((memoryNode2 = memoryNode0 + memoryNode0.UnitCount).Stamp == uint.MaxValue)
                        {
                            memoryNode0.UnitCount = memoryNode0.UnitCount + memoryNode2.UnitCount;
                            memoryNode2.UnitCount = 0;
                        }
                        memoryNode1.Link(memoryNode0);
                        memoryNode1 = memoryNode0;
                    }
                }
            }

            // Coalesce the memory represented by the unused memory nodes.

            while (memoryNode.Available)
            {
                memoryNode0 = memoryNode.Remove();
                uint unitCount = memoryNode0.UnitCount;
                if (unitCount != 0)
                {
                    for (; unitCount > 128; unitCount -= 128, memoryNode0 += 128)
                        MemoryNodes[IndexCount - 1].Insert(memoryNode0, 128);

                    uint index = unitsToIndex[unitCount - 1];
                    if (indexToUnits[index] != unitCount)
                    {
                        uint unitCountDifference = unitCount - indexToUnits[--index];
                        MemoryNodes[unitCountDifference - 1].Insert(memoryNode0 + (unitCount - unitCountDifference), unitCountDifference);
                    }

                    MemoryNodes[index].Insert(memoryNode0, indexToUnits[index]);
                }
            }

            GlueCount = 1 << 13;
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
                target += UnitSize;
                source += UnitSize;
            } while (--unitCount != 0);
        }

        #endregion
    }
}
