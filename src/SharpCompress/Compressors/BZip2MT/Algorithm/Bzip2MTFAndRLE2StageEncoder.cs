// Bzip2 library for .net
// By Jaime Olivares
// Location: http://github.com/jaime-olivares/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2

namespace SharpCompress.Compressors.BZip2MT.Algorithm
{
    /// <summary>An encoder for the BZip2 Move To Front Transform and Run-Length Encoding[2] stages</summary>
    /// <remarks>
    /// An encoder for the BZip2 Move To Front Transform and Run-Length Encoding[2] stages.
    /// Although conceptually these two stages are separate, it is computationally efficient to perform them in one pass.
    /// </remarks>
    internal class BZip2MTFAndRLE2StageEncoder
    {
        // The Burrows-Wheeler transformed block
        private readonly int[] bwtBlock;

        // Actual length of the data in the bwtBlock array
        private readonly int bwtLength;

        // At each position, true if the byte value with that index is present within the block, otherwise false 
        private readonly bool[] bwtValuesInUse;

        // The output of the Move To Front Transform and Run-Length Encoding[2] stages
        private readonly ushort[] mtfBlock;

        // The global frequencies of values within the mtfBlock array
        private readonly int[] mtfSymbolFrequencies = new int[BZip2MTFAndRLE2StageEncoder.HUFFMAN_MAXIMUM_ALPHABET_SIZE];
        
        // Maximum possible Huffman alphabet size
        public const int HUFFMAN_MAXIMUM_ALPHABET_SIZE = 258;

        // Huffman symbol used for run-length encoding
        public const ushort RLE_SYMBOL_RUNA = 0;

        // Huffman symbol used for run-length encoding
        public const ushort RLE_SYMBOL_RUNB = 1;
        
        // Gets the encoded MTF block
        public ushort[] MtfBlock
        {
            get
            {
                return this.mtfBlock;
            }
        }

        // Gets The actual length of the MTF block
        public int MtfLength { get; private set; }

        // Gets the size of the MTF block's alphabet
        public int MtfAlphabetSize { get; private set; }

        // Gets the frequencies of the MTF block's symbols
        public int[] MtfSymbolFrequencies
        {
            get
            {
                return this.mtfSymbolFrequencies;
            }
        }

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="bwtBlock">The Burrows Wheeler Transformed block data</param>
        /// <param name="bwtLength">The actual length of the BWT data</param>
        /// <param name="bwtValuesPresent">The values that are present within the BWT data. For each index,
        /// true if that value is present within the data, otherwise false</param>
        public BZip2MTFAndRLE2StageEncoder(int[] bwtBlock, int bwtLength, bool[] bwtValuesPresent)
        {
            this.bwtBlock = bwtBlock;
            this.bwtLength = bwtLength;
            this.bwtValuesInUse = bwtValuesPresent;
            this.mtfBlock = new ushort[bwtLength + 1];
        }

        // Performs the Move To Front transform and Run Length Encoding[1] stages
        public void Encode()
        {
            var huffmanSymbolMap = new byte[256];
            var symbolMTF = new MoveToFront();

            int totalUniqueValues = 0;
            for (var i = 0; i < 256; i++)
            {
                if (this.bwtValuesInUse[i])
                    huffmanSymbolMap[i] = (byte) totalUniqueValues++;
            }

            int endOfBlockSymbol = totalUniqueValues + 1;
            int mtfIndex = 0;
            int repeatCount = 0;
            int totalRunAs = 0;
            int totalRunBs = 0;

            for (var i = 0; i < this.bwtLength; i++)
            {
                // Move To Front
                int mtfPosition = symbolMTF.ValueToFront(huffmanSymbolMap[this.bwtBlock[i] & 0xff]);

                // Run Length Encode
                if (mtfPosition == 0)
                {
                    repeatCount++;
                } else
                {
                    if (repeatCount > 0)
                    {
                        repeatCount--;
                        while (true)
                        {
                            if ((repeatCount & 1) == 0)
                            {
                                this.mtfBlock[mtfIndex++] = BZip2MTFAndRLE2StageEncoder.RLE_SYMBOL_RUNA;
                                totalRunAs++;
                            } else
                            {
                                this.mtfBlock[mtfIndex++] = BZip2MTFAndRLE2StageEncoder.RLE_SYMBOL_RUNB;
                                totalRunBs++;
                            }

                            if (repeatCount <= 1)
                                break;

                            repeatCount = (repeatCount - 2) >> 1;
                        }
                        repeatCount = 0;
                    }

                    this.mtfBlock[mtfIndex++] = (char) (mtfPosition + 1);
                    this.mtfSymbolFrequencies[mtfPosition + 1]++;
                }
            }

            if (repeatCount > 0)
            {
                repeatCount--;
                while (true)
                {
                    if ((repeatCount & 1) == 0)
                    {
                        this.mtfBlock[mtfIndex++] = BZip2MTFAndRLE2StageEncoder.RLE_SYMBOL_RUNA;
                        totalRunAs++;
                    } else
                    {
                        this.mtfBlock[mtfIndex++] = BZip2MTFAndRLE2StageEncoder.RLE_SYMBOL_RUNB;
                        totalRunBs++;
                    }

                    if (repeatCount <= 1)
                        break;

                    repeatCount = (repeatCount - 2) >> 1;
                }
            }

            this.mtfBlock[mtfIndex] = (ushort)endOfBlockSymbol;
            this.mtfSymbolFrequencies[endOfBlockSymbol]++;
            this.mtfSymbolFrequencies[BZip2MTFAndRLE2StageEncoder.RLE_SYMBOL_RUNA] += totalRunAs;
            this.mtfSymbolFrequencies[BZip2MTFAndRLE2StageEncoder.RLE_SYMBOL_RUNB] += totalRunBs;

            this.MtfLength = mtfIndex + 1;
            this.MtfAlphabetSize = endOfBlockSymbol + 1;

        }
    }
}
