using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Book
{
    public class Huffman16
    {
        private readonly double log2 = Math.Log(2);

        private List<Node> HuffmanTree = new List<Node>();

        internal class Node
        {
            public long Frequency { get; set; }
            public byte Uncoded0 { get; set; }
            public byte Uncoded1 { get; set; }
            public uint Coded { get; set; }
            public int CodeLength { get; set; }
            public Node Left { get; set; }
            public Node Right { get; set; }

            public bool IsLeaf
            {
                get { return Left == null; }
            }

            public override string ToString()
            {
                var coded = "00000000" + Convert.ToString(Coded, 2);
                return string.Format("Uncoded={0}, Coded={1}, Frequency={2}", (Uncoded1 << 8) | Uncoded0, coded.Substring(coded.Length - CodeLength), Frequency);
            }
        }

        public Huffman16(long[] frequencies)
        {
            if (frequencies.Length != ushort.MaxValue + 1)
            {
                throw new ArgumentException("frequencies.Length must equal " + ushort.MaxValue + 1);
            }
            BuildTree(frequencies);
            EncodeTree(HuffmanTree[HuffmanTree.Count - 1], 0, 0);
        }

        public static long[] GetFrequencies(byte[] sampleData, bool safe)
        {
            if (sampleData.Length % 2 != 0)
            {
                throw new ArgumentException("sampleData.Length must be a multiple of 2.");
            }
            var histogram = new long[ushort.MaxValue + 1];
            if (safe)
            {
                for (int i = 0; i <= ushort.MaxValue; i++)
                {
                    histogram[i] = 1;
                }
            }
            for (int i = 0; i < sampleData.Length; i += 2)
            {
                histogram[(sampleData[i] << 8) | sampleData[i + 1]] += 1000;
            }
            return histogram;
        }

        public byte[] Encode(byte[] plainData)
        {
            if (plainData.Length % 2 != 0)
            {
                throw new ArgumentException("plainData.Length must be a multiple of 2.");
            }

            Int64 iBuffer = 0;
            int iBufferCount = 0;

            using (MemoryStream msEncodedOutput = new MemoryStream())
            {
                //Write Final Output Size 1st
                msEncodedOutput.Write(BitConverter.GetBytes(plainData.Length), 0, 4);

                //Begin Writing Encoded Data Stream
                iBuffer = 0;
                iBufferCount = 0;
                for (int i = 0; i < plainData.Length; i += 2)
                {
                    Node FoundLeaf = HuffmanTree[(plainData[i] << 8) | plainData[i + 1]];

                    //How many bits are we adding?
                    iBufferCount += FoundLeaf.CodeLength;

                    //Shift the buffer
                    iBuffer = (iBuffer << FoundLeaf.CodeLength) | FoundLeaf.Coded;

                    //Are there at least 8 bits in the buffer?
                    while (iBufferCount > 7)
                    {
                        //Write to output
                        int iBufferOutput = (int)(iBuffer >> (iBufferCount - 8));
                        msEncodedOutput.WriteByte((byte)iBufferOutput);
                        iBufferCount = iBufferCount - 8;
                        iBufferOutput <<= iBufferCount;
                        iBuffer ^= iBufferOutput;
                    }
                }

                //Write remaining bits in buffer
                if (iBufferCount > 0)
                {
                    iBuffer = iBuffer << (8 - iBufferCount);
                    msEncodedOutput.WriteByte((byte)iBuffer);
                }
                return msEncodedOutput.ToArray();
            }
        }

        public byte[] Decode(byte[] bInput)
        {
            long iInputBuffer = 0;
            int iBytesWritten = 0;

            //Establish Output Buffer to write unencoded data to
            byte[] bDecodedOutput = new byte[BitConverter.ToInt32(bInput, 0)];

            var current = HuffmanTree[HuffmanTree.Count - 1];

            //Begin Looping through Input and Decoding
            iInputBuffer = 0;
            for (int i = 4; i < bInput.Length; i++)
            {
                iInputBuffer = bInput[i];

                for (int bit = 0; bit < 8; bit++)
                {
                    if ((iInputBuffer & 128) == 0)
                    {
                        current = current.Left;
                    }
                    else
                    {
                        current = current.Right;
                    }
                    if (current.IsLeaf)
                    {
                        bDecodedOutput[iBytesWritten++] = current.Uncoded1;
                        bDecodedOutput[iBytesWritten++] = current.Uncoded0;
                        if (iBytesWritten == bDecodedOutput.Length)
                        {
                            return bDecodedOutput;
                        }
                        current = HuffmanTree[HuffmanTree.Count - 1];
                    }
                    iInputBuffer <<= 1;
                }
            }
            throw new Exception();
        }

        private static void EncodeTree(Node node, int depth, uint value)
        {
            if (node != null)
            {
                if (node.IsLeaf)
                {
                    node.CodeLength = depth;
                    node.Coded = value;
                }
                else
                {
                    depth++;
                    value <<= 1;
                    EncodeTree(node.Left, depth, value);
                    EncodeTree(node.Right, depth, value | 1);
                }
            }
        }

        private void BuildTree(long[] frequencies)
        {
            var tiny = 0.1 / ushort.MaxValue;
            var fraction = 0.0;

            SortedDictionary<double, Node> trees = new SortedDictionary<double, Node>();
            for (int i = 0; i <= ushort.MaxValue; i++)
            {
                var leaf = new Node()
                {
                    Uncoded1 = (byte)(i >> 8),
                    Uncoded0 = (byte)(i & 255),
                    Frequency = frequencies[i]
                };
                HuffmanTree.Add(leaf);
                if (leaf.Frequency > 0)
                {
                    trees.Add(leaf.Frequency + (fraction += tiny), leaf);
                }
            }

            while (trees.Count > 1)
            {
                var e = trees.GetEnumerator();
                e.MoveNext();
                var first = e.Current;
                e.MoveNext();
                var second = e.Current;

                //Join smallest two nodes
                var NewParent = new Node();
                NewParent.Frequency = first.Value.Frequency + second.Value.Frequency;
                NewParent.Left = first.Value;
                NewParent.Right = second.Value;

                HuffmanTree.Add(NewParent);

                //Remove the two that just got joined into one
                trees.Remove(first.Key);
                trees.Remove(second.Key);

                trees.Add(NewParent.Frequency + (fraction += tiny), NewParent);
            }
        }

    }

}
