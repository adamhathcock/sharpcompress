// Bzip2 library for .net
// Modified by drone1400
// Location: https://github.com/drone1400/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2
// Modified from the .net implementation by Jaime Olivares: http://github.com/jaime-olivares/bzip2

using System;
using SharpCompress.Compressors.BZip2MT.Interface;
namespace SharpCompress.Compressors.BZip2MT.Algorithm
{
    /// <summary>
    /// An encoder for the BZip2 Huffman encoding stage
    /// </summary>	 
    internal class BZip2HuffmanStageEncoder
    {
        // Used in initial Huffman table generation
        private const int HUFFMAN_HIGH_SYMBOL_COST = 15;

        // The longest Huffman code length created by the encoder
        private const int HUFFMAN_ENCODE_MAXIMUM_CODE_LENGTH = 20;

        // Number of symbols decoded after which a new Huffman table is selected
        public const int HUFFMAN_GROUP_RUN_LENGTH = 50;

        // The BZip2BitOutputStream to which the Huffman tables and data is written
        private readonly IBZip2BitOutputStream bitOutputStream;

        // The output of the Move To Front Transform and Run Length Encoding[2] stages
        private readonly ushort[] mtfBlock;

        // The actual number of values contained in the mtfBlock array
        private readonly int mtfLength;

        // The number of unique values in the mtfBlock array
        private readonly int mtfAlphabetSize;

        // The global frequencies of values within the mtfBlock array
        private readonly int[] mtfSymbolFrequencies;

        // The Canonical Huffman code lengths for each table
        private readonly int[,] huffmanCodeLengths;

        // Merged code symbols for each table. The value at each position is ((code length << 24) | code)
        private readonly int[,] huffmanMergedCodeSymbols;

        // The selectors for each segment
        private readonly byte[] selectors;
        
        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="bitOutputStream">The BZip2BitOutputStream to write to</param>
        /// <param name="mtfBlock">The MTF block data</param>
        /// <param name="mtfLength">The actual length of the MTF block</param>
        /// <param name="mtfAlphabetSize">The size of the MTF block's alphabet</param>
        /// <param name="mtfSymbolFrequencies">The frequencies the MTF block's symbols</param>
        public BZip2HuffmanStageEncoder(IBZip2BitOutputStream bitOutputStream, ushort[] mtfBlock, int mtfLength, int mtfAlphabetSize, int[] mtfSymbolFrequencies)
        {
            this.bitOutputStream = bitOutputStream;
            this.mtfBlock = mtfBlock;
            this.mtfSymbolFrequencies = mtfSymbolFrequencies;
            this.mtfAlphabetSize = mtfAlphabetSize;
            this.mtfLength = mtfLength;

            var totalTables = selectTableCount(mtfLength);

            this.huffmanCodeLengths = new int[totalTables, mtfAlphabetSize];
            this.huffmanMergedCodeSymbols = new int[totalTables, mtfAlphabetSize];
            this.selectors = new byte[(mtfLength + BZip2HuffmanStageEncoder.HUFFMAN_GROUP_RUN_LENGTH - 1) / BZip2HuffmanStageEncoder.HUFFMAN_GROUP_RUN_LENGTH];
        }

        /// <summary>
        /// Encodes and writes the block data
        /// </summary>
        /// <exception cref="Exception">on any I/O error writing the data</exception>
        public void Encode()
        {
            // Create optimised selector list and Huffman tables
            this.generateHuffmanOptimisationSeeds();
            for (var i = 3; i >= 0; i--)
            {
                this.optimiseSelectorsAndHuffmanTables(i == 0);
            }
            this.assignHuffmanCodeSymbols();

            // Write out the tables and the block data encoded with them
            this.writeSelectorsAndHuffmanTables();
            this.writeBlockData();
        }

        /// <summary>
        /// Selects an appropriate table count for a given MTF length
        /// </summary>
        /// <param name="mtfLength">The length to select a table count for</param>
        /// <returns>The selected table count</returns>
        private static int selectTableCount (int mtfLength)
        {
            if (mtfLength >= 2400)
                return 6;
            if (mtfLength >= 1200)
                return 5;
            if (mtfLength >= 600)
                return 4;
            return mtfLength >= 200 ? 3 : 2;
        }

        /// <summary>
        /// Generate a Huffman code length table for a given list of symbol frequencies
        /// </summary>
        /// <param name="alphabetSize">The total number of symbols</param>
        /// <param name="symbolFrequencies">The frequencies of the symbols</param>
        /// <param name="codeLengths">The array to which the generated code lengths should be written</param>
        /// <param name="index"></param>
        private static void generateHuffmanCodeLengths (int alphabetSize,  int[,] symbolFrequencies, int[,] codeLengths, int index)
        {
            var mergedFrequenciesAndIndices = new int[alphabetSize];
            var sortedFrequencies = new int[alphabetSize];

            // The Huffman allocator needs its input symbol frequencies to be sorted, but we need to return code lengths in the same order as the
            // corresponding frequencies are passed in

            // The symbol frequency and index are merged into a single array of integers - frequency in the high 23 bits, index in the low 9 bits.
            //     2^23 = 8,388,608 which is higher than the maximum possible frequency for one symbol in a block
            //     2^9 = 512 which is higher than the maximum possible alphabet size (== 258)
            // Sorting this array simultaneously sorts the frequencies and leaves a lookup that can be used to cheaply invert the sort
            for (int i = 0; i < alphabetSize; i++)
                mergedFrequenciesAndIndices[i] = (symbolFrequencies[index, i] << 9) | i;

            Array.Sort(mergedFrequenciesAndIndices);
            for (int i = 0; i < alphabetSize; i++)
                sortedFrequencies[i] = mergedFrequenciesAndIndices[i] >> 9;

            // Allocate code lengths - the allocation is in place, so the code lengths will be in the sortedFrequencies array afterwards
            HuffmanAllocator.AllocateHuffmanCodeLengths (sortedFrequencies, BZip2HuffmanStageEncoder.HUFFMAN_ENCODE_MAXIMUM_CODE_LENGTH);

            // Reverse the sort to place the code lengths in the same order as the symbols whose frequencies were passed in
            for (int i = 0; i < alphabetSize; i++)
                codeLengths[index, mergedFrequenciesAndIndices[i] & 0x1ff] = sortedFrequencies[i];
        }

        /// <summary>
        /// Generate initial Huffman code length tables, giving each table a different low cost section
        /// of the alphabet that is roughly equal in overall cumulative frequency. Note that the initial
        /// tables are invalid for actual Huffman code generation, and only serve as the seed for later
        /// iterative optimisation in optimiseSelectorsAndHuffmanTables(int)
        /// </summary>
        private void generateHuffmanOptimisationSeeds ()
        {
            int totalTables = this.huffmanCodeLengths.GetLength(0);
            int remainingLength = this.mtfLength;
            int lowCostEnd = -1;

            for (int i = 0; i < totalTables; i++)
            {
                int targetCumulativeFrequency = remainingLength / (totalTables - i);
                int lowCostStart = lowCostEnd + 1;
                int actualCumulativeFrequency = 0;

                while ((actualCumulativeFrequency < targetCumulativeFrequency) && (lowCostEnd < (this.mtfAlphabetSize - 1)))
                {
                    actualCumulativeFrequency += this.mtfSymbolFrequencies[++lowCostEnd];
                }

                if ((lowCostEnd > lowCostStart) && (i != 0) && (i != (totalTables - 1)) && (((totalTables - i) & 1) == 0))
                    actualCumulativeFrequency -= this.mtfSymbolFrequencies[lowCostEnd--];

                for (var j = 0; j < this.mtfAlphabetSize; j++)
                {
                    if ((j < lowCostStart) || (j > lowCostEnd))
                        this.huffmanCodeLengths[i, j] = BZip2HuffmanStageEncoder.HUFFMAN_HIGH_SYMBOL_COST;
                }

                remainingLength -= actualCumulativeFrequency;
            }
        }

        /// <summary>
        /// Co-optimise the selector list and the alternative Huffman table code lengths. This method is
        /// called repeatedly in the hope that the total encoded size of the selectors, the Huffman code
        /// lengths and the block data encoded with them will converge towards a minimum. <br/>
        /// If the data is highly incompressible, it is possible that the total encoded size will
        /// instead diverge (increase) slightly. <br/>
        /// </summary>
        /// <param name="storeSelectors">If true, write out the (final) chosen selectors</param>
        private void optimiseSelectorsAndHuffmanTables (bool storeSelectors)
        {
            int totalTables = this.huffmanCodeLengths.GetLength(0);
            var tableFrequencies = new int[totalTables, this.mtfAlphabetSize];

            int selectorIndex = 0;

            // Find the best table for each group of 50 block bytes based on the current Huffman code lengths
            for (int groupStart = 0; groupStart < this.mtfLength;)
            {
                int groupEnd = Math.Min(groupStart + BZip2HuffmanStageEncoder.HUFFMAN_GROUP_RUN_LENGTH, this.mtfLength) - 1;

                // Calculate the cost of this group when encoded by each table
                var cost = new int[totalTables];
                for (int i = groupStart; i <= groupEnd; i++)
                {
                    int value = this.mtfBlock[i];
                    for (var j = 0; j < totalTables; j++)
                    {
                        cost[j] += this.huffmanCodeLengths[j, value];
                    }
                }

                // Find the table with the least cost for this group
                byte bestTable = 0;
                var bestCost = cost[0];
                for (byte i = 1 ; i < totalTables; i++)
                {
                    var tableCost = cost[i];
                    if (tableCost < bestCost)
                    {
                        bestCost = tableCost;
                        bestTable = i;
                    }
                }

                // Accumulate symbol frequencies for the table chosen for this block
                for (int i = groupStart; i <= groupEnd; i++)
                {
                    tableFrequencies[bestTable, this.mtfBlock[i]]++;
                }

                // Store a selector indicating the table chosen for this block
                if (storeSelectors)
                {
                    this.selectors[selectorIndex++] = bestTable;
                }

                groupStart = groupEnd + 1;
            }

            // Generate new Huffman code lengths based on the frequencies for each table accumulated in this iteration
            for (int i = 0; i < totalTables; i++)
            {
                generateHuffmanCodeLengths (this.mtfAlphabetSize, tableFrequencies, this.huffmanCodeLengths, i);
            }
        }

        // Assigns Canonical Huffman codes based on the calculated lengths
        private void assignHuffmanCodeSymbols()
        {
            int totalTables = this.huffmanCodeLengths.GetLength(0);

            for (int i = 0; i < totalTables; i++)
            {
                int minimumLength = 32;
                int maximumLength = 0;
                for (var j = 0; j < this.mtfAlphabetSize; j++)
                {
                    var length = this.huffmanCodeLengths[i, j];
                    if (length > maximumLength)
                    {
                        maximumLength = length;
                    }
                    if (length < minimumLength)
                    {
                        minimumLength = length;
                    }
                }

                int code = 0;
                for (int j = minimumLength; j <= maximumLength; j++)
                {
                    for (var k = 0; k < this.mtfAlphabetSize; k++)
                    {
                        if ((this.huffmanCodeLengths[i, k] & 0xff) == j)
                        {
                            this.huffmanMergedCodeSymbols[i, k] = (j << 24) | code;
                            code++;
                        }
                    }
                    code <<= 1;
                }
            }
        }

        /// <summary>
        /// Write out the selector list and Huffman tables
        /// </summary>
        /// <exception cref="Exception">on any I/O error writing the data</exception>
        private void writeSelectorsAndHuffmanTables()
        {
            int totalSelectors = this.selectors.Length;
            int totalTables = this.huffmanCodeLengths.GetLength(0);

            this.bitOutputStream.WriteBits (3, (uint)totalTables);
            this.bitOutputStream.WriteBits (15, (uint)totalSelectors);

            // Write the selectors
            var selectorMTF = new MoveToFront();
            for (int i = 0; i < totalSelectors; i++)
            {
                this.bitOutputStream.WriteUnary (selectorMTF.ValueToFront (this.selectors[i]));
            }

            // Write the Huffman tables
            for (int i = 0; i < totalTables; i++)
            {
                var currentLength = this.huffmanCodeLengths[i, 0];

                this.bitOutputStream.WriteBits (5, (uint)currentLength);

                for (var j = 0; j < this.mtfAlphabetSize; j++)
                {
                    var codeLength = this.huffmanCodeLengths[i, j];
                    var value = (currentLength < codeLength) ? 2u : 3u;
                    var delta = Math.Abs (codeLength - currentLength);
                    while (delta-- > 0)
                    {
                        this.bitOutputStream.WriteBits (2, value);
                    }
                    this.bitOutputStream.WriteBoolean (false);
                    currentLength = codeLength;
                }
            }
        }

        /// <summary>
        /// Writes out the encoded block data
        /// </summary>
        /// <exception cref="Exception">on any I/O error writing the data</exception>
        private void writeBlockData()
        {
            int selectorIndex = 0;

            for (int mtfIndex = 0; mtfIndex < this.mtfLength;)
            {
                int groupEnd = Math.Min (mtfIndex + BZip2HuffmanStageEncoder.HUFFMAN_GROUP_RUN_LENGTH, this.mtfLength) - 1;

                int index = this.selectors [selectorIndex++];

                while (mtfIndex <= groupEnd)
                {
                    var mergedCodeSymbol = this.huffmanMergedCodeSymbols[index, this.mtfBlock[mtfIndex++]];
                    this.bitOutputStream.WriteBits(mergedCodeSymbol >> 24, (uint)mergedCodeSymbol);
                }
            }
        }
    }
}
