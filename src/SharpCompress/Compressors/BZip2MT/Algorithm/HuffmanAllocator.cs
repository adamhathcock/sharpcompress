// Bzip2 library for .net
// By Jaime Olivares
// Location: http://github.com/jaime-olivares/bzip2
// Ported from the Java implementation by Matthew Francis: https://github.com/MateuszBartosiewicz/bzip2

using System;
namespace SharpCompress.Compressors.BZip2MT.Algorithm
{
    /// <summary>An in-place, length restricted Canonical Huffman code length allocator</summary>
    /// <remarks>
    /// Based on the algorithm proposed by R.L.Milidiú, A.A.Pessoa and E.S.Laber 
    /// in "In-place Length-Restricted Prefix Coding" (see: http://www-di.inf.puc-rio.br/~laber/public/spire98.ps)
    /// and incorporating additional ideas from the implementation of "shcodec" by Simakov Alexander
    /// (see: http://webcenter.ru/~xander/)
    /// </remarks>
    internal static class HuffmanAllocator
    {
        /// <summary>Allocates Canonical Huffman code lengths in place based on a sorted frequency array</summary>
        /// <param name="array">On input, a sorted array of symbol frequencies; On output, an array of Canonical Huffman code lenghts</param>
        /// <param name="maximumLength">The maximum code length. Must be at least ceil(log2(array.length))</param>
        public static void AllocateHuffmanCodeLengths(int[] array, int maximumLength)
        {
            switch (array.Length)
            {
                case 2:
                    array[1] = 1;
                    break;
                case 1:
                    array[0] = 1;
                    return;
            }

            // Pass 1 : Set extended parent pointers
            SetExtendedParentPointers(array);

            // Pass 2 : Find number of nodes to relocate in order to achieve maximum code length
            int nodesToRelocate = FindNodesToRelocate(array, maximumLength);

            // Pass 3 : Generate code lengths
            if ((array[0] % array.Length) >= nodesToRelocate)
            {
                AllocateNodeLengths(array);
            } else
            {
                var insertDepth = maximumLength - SignificantBits(nodesToRelocate - 1);
                AllocateNodeLengthsWithRelocation(array, nodesToRelocate, insertDepth);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="array">The code length array</param>
        /// <param name="i">The input position</param>
        /// <param name="nodesToMove">The number of internal nodes to be relocated</param>
        /// <returns>The smallest k such that nodesToMove &lt;= k &lt;= i and i &lt;= (array[k] % array.length)</returns>
        private static int First (int[] array, int i,  int nodesToMove)
        {
            int length = array.Length;
            int limit = i;
            int k = array.Length - 2;

            while ((i >= nodesToMove) && ((array[i] % length) > limit))
            {
                k = i;
                i -= (limit - i + 1);
            }
            i = Math.Max (nodesToMove - 1, i);

            while (k > (i + 1))
            {
                var temp = (i + k) >> 1;
                if ((array[temp] % length) > limit)
                    k = temp;
                else
                    i = temp;
            }

            return k;
        }

        /// <summary>
        /// Fills the code array with extended parent pointers
        /// </summary>
        /// <param name="array">The code length array</param>
        private static void SetExtendedParentPointers(int[] array)
        {
            int length = array.Length;

            array[0] += array[1];

            for (int headNode = 0, tailNode = 1, topNode = 2; tailNode < (length - 1); tailNode++)
            {
                int temp;
                if ((topNode >= length) || (array[headNode] < array[topNode]))
                {
                    temp = array[headNode];
                    array[headNode++] = tailNode;
                } else
                {
                    temp = array[topNode++];
                }

                if ((topNode >= length) || ((headNode < tailNode) && (array[headNode] < array[topNode])))
                {
                    temp += array[headNode];
                    array[headNode++] = tailNode + length;
                } else
                {
                    temp += array[topNode++];
                }

                array[tailNode] = temp;
            }
        }

        /// <summary>
        /// Finds the number of nodes to relocate in order to achieve a given code length limit
        /// </summary>
        /// <param name="array">The code length array</param>
        /// <param name="maximumLength">The maximum bit length for the generated codes</param>
        /// <returns>The number of nodes to relocate</returns>
        private static int FindNodesToRelocate (int[] array,  int maximumLength)
        {
            int currentNode = array.Length - 2;

            for (var currentDepth = 1; (currentDepth < (maximumLength - 1)) && (currentNode > 1); currentDepth++)
                currentNode =  First (array, currentNode - 1, 0);

            return currentNode;
        }

        /// <summary>
        /// A final allocation pass with no code length limit
        /// </summary>
        /// <param name="array">The code length array</param>
        private static void AllocateNodeLengths (int[] array)
        {
            int firstNode = array.Length - 2;
            int nextNode = array.Length - 1;

            for (int currentDepth = 1, availableNodes = 2; availableNodes > 0; currentDepth++)
            {
                int lastNode = firstNode;
                firstNode = First (array, lastNode - 1, 0);

                for (var i = availableNodes - (lastNode - firstNode); i > 0; i--)
                    array[nextNode--] = currentDepth;

                availableNodes = (lastNode - firstNode) << 1;
            }
        }

        /// <summary>
        /// A final allocation pass that relocates nodes in order to achieve a maximum code length limit
        /// </summary>
        /// <param name="array">The code length array</param>
        /// <param name="nodesToMove">The number of internal nodes to be relocated</param>
        /// <param name="insertDepth">The depth at which to insert relocated nodes</param>
        private static void AllocateNodeLengthsWithRelocation (int[] array,  int nodesToMove,  int insertDepth)
        {
            int firstNode = array.Length - 2;
            int nextNode = array.Length - 1;
            int currentDepth = (insertDepth == 1) ? 2 : 1;
            int nodesLeftToMove = (insertDepth == 1) ? nodesToMove - 2 : nodesToMove;

            for (int availableNodes = currentDepth << 1; availableNodes > 0; currentDepth++)
            {
                int lastNode = firstNode;
                firstNode = (firstNode <= nodesToMove) ? firstNode : First (array, lastNode - 1, nodesToMove);

                int offset = 0;
                if (currentDepth >= insertDepth)
                {
                    offset = Math.Min (nodesLeftToMove, 1 << (currentDepth - insertDepth));
                } else if (currentDepth == (insertDepth - 1))
                {
                    offset = 1;
                    if ((array[firstNode]) == lastNode)
                        firstNode++;
                }

                for (var i = availableNodes - (lastNode - firstNode + offset); i > 0; i--)
                    array[nextNode--] = currentDepth;

                nodesLeftToMove -= offset;
                availableNodes = (lastNode - firstNode + offset) << 1;
            }
        }

        private static int SignificantBits(int x)
        {
            int n;
            for (n = 0; x > 0; n++)
            {
                x >>= 1;
            }
            return n;
        }
    }
}
