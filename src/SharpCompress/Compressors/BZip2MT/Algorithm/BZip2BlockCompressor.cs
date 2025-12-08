// Bzip2 library for .net
// Modified by drone1400
// Location: https://github.com/drone1400/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2
// Modified from the .net implementation by Jaime Olivares: http://github.com/jaime-olivares/bzip2

using System;
using SharpCompress.Compressors.BZip2MT.Interface;

namespace SharpCompress.Compressors.BZip2MT.Algorithm
{
    /// <summary>Compresses and writes a single BZip2 block</summary>
    /// <remarks>
    /// Block encoding consists of the following stages:
    /// 1. Run-Length Encoding[1] - write()
    /// 2. Burrows Wheeler Transform - close() (through BZip2DivSufSort)
    /// 3. Write block header - close()
    /// 4. Move To Front Transform - close() (through BZip2HuffmanStageEncoder)
    /// 5. Run-Length Encoding[2] - close()  (through BZip2HuffmanStageEncoder)
    /// 6. Create and write Huffman tables - close() (through BZip2HuffmanStageEncoder)
    /// 7. Huffman encode and write data - close() (through BZip2HuffmanStageEncoder)
    /// </remarks>
    internal class BZip2BlockCompressor
    {
        // The stream to which compressed BZip2 data is written
        private readonly IBZip2BitOutputStream bitOutputStream;

        // CRC builder for the block
        private readonly CRC32 crc = new CRC32();

        // The RLE'd block data
        private readonly byte[] block;

        // Current length of the data within the block array
        private int blockLength;

        // A limit beyond which new data will not be accepted into the block
        private readonly int blockLengthLimit;

        // The values that are present within the RLE'd block data. For each index, true if that
        // value is present within the data, otherwise false
        private readonly bool[] blockValuesPresent = new bool[256];

        // The Burrows Wheeler Transformed block data
        private readonly int[] bwtBlock;

        // The current RLE value being accumulated (undefined when rleLength is 0)
        private int rleCurrentValue = -1;

        // The repeat count of the current RLE value
        private int rleLength;

        /// <summary>
        /// Determines if any bytes have been written to the block.
        /// True if one or more bytes has been written to the block, otherwise false.
        /// </summary>
        public bool IsEmpty
        {
            get { return ((this.blockLength == 0) && (this.rleLength == 0)); }
        }

        /// <summary>
        /// Gets the CRC of the completed block. Only valid after calling Close().
        /// </summary>
        public uint CRC
        {
            get { return this.crc.CRC; }
        }

        /// <summary>
        /// Public constructor
        /// </summary>
        /// <param name="bitOutputStream">The BZip2BitOutputStream to which compressed BZip2 data is written</param>
        /// <param name="blockSize">The declared block size in bytes. Up to this many bytes will be accepted
        /// into the block after Run-Length Encoding is applied</param>
        public BZip2BlockCompressor(IBZip2BitOutputStream bitOutputStream, int blockSize)
        {
            this.bitOutputStream = bitOutputStream;

            // One extra byte is added to allow for the block wrap applied in close()
            this.block = new byte[blockSize + 1];
            this.bwtBlock = new int[blockSize + 1];
            this.blockLengthLimit = blockSize - 6; // 5 bytes for one RLE run plus one byte - see Write(int)
        }

        /// <summary>
        /// Writes a byte to the block, accumulating to an RLE run where possible
        /// </summary>
        /// <param name="value">The byte to write</param>
        /// <returns>True if the byte was written, or false if the block is already full</returns>
        public bool Write(int value)
        {
            if (this.blockLength > this.blockLengthLimit)
                return false;

            if (this.rleLength == 0)
            {
                this.rleCurrentValue = value;
                this.rleLength = 1;
            }
            else if (this.rleCurrentValue != value)
            {
                // This path commits us to write 6 bytes - one RLE run (5 bytes) plus one extra
                this.WriteRun(this.rleCurrentValue & 0xff, this.rleLength);
                this.rleCurrentValue = value;
                this.rleLength = 1;
            }
            else
            {
                if (this.rleLength == 254)
                {
                    this.WriteRun(this.rleCurrentValue & 0xff, 255);
                    this.rleLength = 0;
                }
                else
                {
                    this.rleLength++;
                }
            }

            return true;
        }

        /// <summary>
        /// Writes an array to the block
        /// </summary>
        /// <param name="data">The array to write</param>
        /// <param name="offset">The offset within the input data to write from</param>
        /// <param name="length">The number of bytes of input data to write</param>
        /// <returns>The actual number of input bytes written. May be less than the number requested, or
        /// zero if the block is already full</returns>
        public int Write(byte[] data, int offset, int length)
        {
            var written = 0;

            while (length-- > 0)
            {
                if (!this.Write(data[offset++]))
                    break;
                written++;
            }

            return written;
        }

        /// <summary>Compresses and writes out the block.</summary>
        /// <exception cref="Exception">Exception on any I/O error writing the data</exception>
        public void CloseBlock()
        {
            // If an RLE run is in progress, write it out
            if (this.rleLength > 0)
                this.WriteRun(this.rleCurrentValue & 0xff, this.rleLength);

            // Apply a one byte block wrap required by the BWT implementation
            this.block[this.blockLength] = this.block[0];

            // Perform the Burrows Wheeler Transform
            var divSufSort = new BZip2DivSufSort(this.block, this.bwtBlock, this.blockLength);
            var bwtStartPointer = divSufSort.BWT();

            // Write out the block header
            this.bitOutputStream.WriteBits(24, BZip2Constants.BLOCK_HEADER_MARKER_1);
            this.bitOutputStream.WriteBits(24, BZip2Constants.BLOCK_HEADER_MARKER_2);
            this.bitOutputStream.WriteInteger(this.crc.CRC);
            this.bitOutputStream.WriteBoolean(false); // Randomised block flag. We never create randomised blocks
            this.bitOutputStream.WriteBits(24, (uint)bwtStartPointer);

            // Write out the symbol map
            this.WriteSymbolMap();

            // Perform the Move To Front Transform and Run-Length Encoding[2] stages
            var mtfEncoder = new BZip2MTFAndRLE2StageEncoder(
                this.bwtBlock,
                this.blockLength,
                this.blockValuesPresent
            );
            mtfEncoder.Encode();

            // Perform the Huffman Encoding stage and write out the encoded data
            var huffmanEncoder = new BZip2HuffmanStageEncoder(
                this.bitOutputStream,
                mtfEncoder.MtfBlock,
                mtfEncoder.MtfLength,
                mtfEncoder.MtfAlphabetSize,
                mtfEncoder.MtfSymbolFrequencies
            );
            huffmanEncoder.Encode();
        }

        /// <summary>
        /// Write the Huffman symbol to output byte map
        /// </summary>
        /// <exception cref="Exception">on any I/O error writing the data</exception>
        private void WriteSymbolMap()
        {
            var condensedInUse = new bool[16];

            for (var i = 0; i < 16; i++)
            {
                for (int j = 0, k = i << 4; j < 16; j++, k++)
                {
                    if (this.blockValuesPresent[k])
                    {
                        condensedInUse[i] = true;
                    }
                }
            }

            for (var i = 0; i < 16; i++)
            {
                this.bitOutputStream.WriteBoolean(condensedInUse[i]);
            }

            for (var i = 0; i < 16; i++)
            {
                if (condensedInUse[i])
                {
                    for (int j = 0, k = i * 16; j < 16; j++, k++)
                    {
                        this.bitOutputStream.WriteBoolean(this.blockValuesPresent[k]);
                    }
                }
            }
        }

        /// <summary>
        /// Writes an RLE run to the block array, updating the block CRC and present values array as required
        /// </summary>
        /// <param name="value">The value to write</param>
        /// <param name="runLength">The run length of the value to write</param>
        private void WriteRun(int value, int runLength)
        {
            this.blockValuesPresent[value] = true;
            this.crc.UpdateCrc(value, runLength);

            var byteValue = (byte)value;
            switch (runLength)
            {
                case 1:
                    this.block[this.blockLength] = byteValue;
                    this.blockLength = this.blockLength + 1;
                    break;

                case 2:
                    this.block[this.blockLength] = byteValue;
                    this.block[this.blockLength + 1] = byteValue;
                    this.blockLength = this.blockLength + 2;
                    break;

                case 3:
                    this.block[this.blockLength] = byteValue;
                    this.block[this.blockLength + 1] = byteValue;
                    this.block[this.blockLength + 2] = byteValue;
                    this.blockLength = this.blockLength + 3;
                    break;

                default:
                    runLength -= 4;
                    this.blockValuesPresent[runLength] = true;
                    this.block[this.blockLength] = byteValue;
                    this.block[this.blockLength + 1] = byteValue;
                    this.block[this.blockLength + 2] = byteValue;
                    this.block[this.blockLength + 3] = byteValue;
                    this.block[this.blockLength + 4] = (byte)runLength;
                    this.blockLength = this.blockLength + 5;
                    break;
            }
        }
    }
}
