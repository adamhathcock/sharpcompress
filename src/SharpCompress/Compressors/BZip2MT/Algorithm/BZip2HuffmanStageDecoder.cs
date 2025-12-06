// Bzip2 library for .net
// By Jaime Olivares
// Location: http://github.com/jaime-olivares/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2

using System;
using System.IO;
using SharpCompress.Compressors.BZip2MT.Interface;
namespace SharpCompress.Compressors.BZip2MT.Algorithm
{
    /// <summary>
    /// A decoder for the BZip2 Huffman coding stage
    /// </summary>
    internal class BZip2HuffmanStageDecoder
    {
        // The BZip2BitInputStream from which Huffman codes are read
        private readonly IBZip2BitInputStream bitInputStream;

        // The longest Huffman code length accepted by the decoder
        private const int HUFFMAN_DECODE_MAXIMUM_CODE_LENGTH = 23;

        // The Huffman table number to use for each group of 50 symbols
        private readonly byte[] selectors;

        // The minimum code length for each Huffman table
        private readonly int[] minimumLengths = new int[BZip2BlockDecompressor.HUFFMAN_MAXIMUM_TABLES];

        /// <summary>
        /// An array of values for each Huffman table that must be subtracted from the numerical value of
        /// a Huffman code of a given bit length to give its canonical code index
        /// </summary>
        private readonly int[,] codeBases = new int[BZip2BlockDecompressor.HUFFMAN_MAXIMUM_TABLES, BZip2HuffmanStageDecoder.HUFFMAN_DECODE_MAXIMUM_CODE_LENGTH + 2];

        /// <summary>
        /// An array of values for each Huffman table that gives the highest numerical value of a Huffman
        /// code of a given bit length
        /// </summary>
        private readonly int[,] codeLimits = new int[BZip2BlockDecompressor.HUFFMAN_MAXIMUM_TABLES, BZip2HuffmanStageDecoder.HUFFMAN_DECODE_MAXIMUM_CODE_LENGTH + 1];

        // A mapping for each Huffman table from canonical code index to output symbol
        private readonly int[,] codeSymbols = new int[BZip2BlockDecompressor.HUFFMAN_MAXIMUM_TABLES, BZip2MTFAndRLE2StageEncoder.HUFFMAN_MAXIMUM_ALPHABET_SIZE];

        // The Huffman table for the current group
        private int currentTable;

        // The index of the current group within the selectors array
        private int groupIndex = -1;

        // The byte position within the current group. A new group is selected every 50 decoded bytes
        private int groupPosition = -1;

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="bitInputStream">The BZip2BitInputStream from which Huffman codes are read</param>
        /// <param name="alphabetSize">The total number of codes (uniform for each table)</param>
        /// <param name="tableCodeLengths">The Canonical Huffman code lengths for each table</param>
        /// <param name="selectors">The Huffman table number to use for each group of 50 symbols</param>
        public BZip2HuffmanStageDecoder(IBZip2BitInputStream bitInputStream, int alphabetSize, byte[,] tableCodeLengths, byte[] selectors)
        {
            this.bitInputStream = bitInputStream;
            this.selectors = selectors;
            this.currentTable = this.selectors[0];
            this.CreateHuffmanDecodingTables(alphabetSize, tableCodeLengths);
        }

        /// <summary>
        /// Decodes and returns the next symbol
        /// </summary>
        /// <returns>The decoded symbol</returns>
        /// <exception cref="IOException">if the end of the input stream is reached while decoding</exception>
        public int NextSymbol()
        {
            // Move to next group selector if required
            if (((++this.groupPosition % BZip2HuffmanStageEncoder.HUFFMAN_GROUP_RUN_LENGTH) == 0))
            {
                this.groupIndex++;
                if (this.groupIndex == this.selectors.Length)
                    throw new IOException("Error decoding BZip2 block");

                this.currentTable = this.selectors[this.groupIndex] & 0xff;
            }

            var codeLength = this.minimumLengths[this.currentTable];

            // Starting with the minimum bit length for the table, read additional bits one at a time
            // until a complete code is recognised
            for (uint codeBits = this.bitInputStream.ReadBits(codeLength); codeLength <= BZip2HuffmanStageDecoder.HUFFMAN_DECODE_MAXIMUM_CODE_LENGTH; codeLength++)
            {
                if (codeBits <= this.codeLimits[this.currentTable, codeLength])
                {
                    // Convert the code to a symbol index and return
                    return this.codeSymbols[this.currentTable, codeBits - this.codeBases[this.currentTable, codeLength]];
                }
                codeBits = (codeBits << 1) | this.bitInputStream.ReadBits(1);
            }

            // A valid code was not recognised
            throw new IOException("Error decoding BZip2 block");
        }
        
        /// <summary>
        /// Constructs Huffman decoding tables from lists of Canonical Huffman code lengths
        /// </summary>
        /// <param name="alphabetSize">The total number of codes (uniform for each table)</param>
        /// <param name="tableCodeLengths">The Canonical Huffman code lengths for each table</param>
        private void CreateHuffmanDecodingTables (int alphabetSize,  byte[,] tableCodeLengths)
        {

            for (int table = 0; table < tableCodeLengths.GetLength(0); table++)
            {
                int minimumLength = BZip2HuffmanStageDecoder.HUFFMAN_DECODE_MAXIMUM_CODE_LENGTH;
                int maximumLength = 0;

                // Find the minimum and maximum code length for the table
                for (int i = 0; i < alphabetSize; i++)
                {
                    maximumLength = Math.Max (tableCodeLengths[table, i], maximumLength);
                    minimumLength = Math.Min (tableCodeLengths[table, i], minimumLength);
                }
                this.minimumLengths[table] = minimumLength;

                // Calculate the first output symbol for each code length
                for (int i = 0; i < alphabetSize; i++)
                {
                    this.codeBases[table, tableCodeLengths[table, i] + 1]++;
                }
                for (int i = 1; i < BZip2HuffmanStageDecoder.HUFFMAN_DECODE_MAXIMUM_CODE_LENGTH + 2; i++)
                {
                    this.codeBases[table, i] += this.codeBases[table, i - 1];
                }

                // Calculate the first and last Huffman code for each code length (codes at a given length are sequential in value)
                for (int i = minimumLength, code = 0; i <= maximumLength; i++)
                {
                    int base1 = code;
                    code += this.codeBases[table, i + 1] - this.codeBases[table, i];
                    this.codeBases[table, i] = base1 - this.codeBases[table, i];
                    this.codeLimits[table, i] = code - 1;
                    code <<= 1;
                }

                // Populate the mapping from canonical code index to output symbol
                for (int bitLength = minimumLength, codeIndex = 0; bitLength <= maximumLength; bitLength++)
                {
                    for (int symbol = 0; symbol < alphabetSize; symbol++)
                    {
                        if (tableCodeLengths[table, symbol] == bitLength)
                            this.codeSymbols[table, codeIndex++] = symbol;
                    }
                }
            }
        }
    }
}
