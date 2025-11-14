using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpCompress.Compressors.Arj
{
    [CLSCompliant(true)]
    public enum NodeType
    {
        Leaf,
        Branch,
    }

    [CLSCompliant(true)]
    public sealed class TreeEntry
    {
        public readonly NodeType Type;
        public readonly int LeafValue;
        public readonly int BranchIndex;

        public const int MAX_INDEX = 4096;

        private TreeEntry(NodeType type, int leafValue, int branchIndex)
        {
            Type = type;
            LeafValue = leafValue;
            BranchIndex = branchIndex;
        }

        public static TreeEntry Leaf(int value)
        {
            return new TreeEntry(NodeType.Leaf, value, -1);
        }

        public static TreeEntry Branch(int index)
        {
            if (index >= MAX_INDEX)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    "Branch index exceeds MAX_INDEX"
                );
            }
            return new TreeEntry(NodeType.Branch, 0, index);
        }
    }

    [CLSCompliant(true)]
    public sealed class HuffTree
    {
        private readonly List<TreeEntry> _tree;

        public HuffTree(int capacity = 0)
        {
            _tree = new List<TreeEntry>(capacity);
        }

        public void SetSingle(int value)
        {
            _tree.Clear();
            _tree.Add(TreeEntry.Leaf(value));
        }

        public void BuildTree(byte[] lengths, int count)
        {
            if (lengths == null)
            {
                throw new ArgumentNullException(nameof(lengths));
            }

            if (count < 0 || count > lengths.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count > TreeEntry.MAX_INDEX / 2)
            {
                throw new ArgumentException(
                    $"Count exceeds maximum allowed: {TreeEntry.MAX_INDEX / 2}"
                );
            }
            byte[] slice = new byte[count];
            Array.Copy(lengths, slice, count);

            BuildTree(slice);
        }

        public void BuildTree(byte[] valueLengths)
        {
            if (valueLengths == null)
            {
                throw new ArgumentNullException(nameof(valueLengths));
            }

            if (valueLengths.Length > TreeEntry.MAX_INDEX / 2)
            {
                throw new InvalidOperationException("Too many code lengths");
            }

            _tree.Clear();

            int maxAllocated = 1; // start with a single (root) node

            for (byte currentLen = 1; ; currentLen++)
            {
                // add missing branches up to current limit
                int maxLimit = maxAllocated;

                for (int i = _tree.Count; i < maxLimit; i++)
                {
                    // TreeEntry.Branch may throw if index too large
                    try
                    {
                        _tree.Add(TreeEntry.Branch(maxAllocated));
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        _tree.Clear();
                        throw new InvalidOperationException("Branch index exceeds limit", e);
                    }

                    // each branch node allocates two children
                    maxAllocated += 2;
                }

                // fill tree with leaves found in the lengths table at the current length
                bool moreLeaves = false;

                for (int value = 0; value < valueLengths.Length; value++)
                {
                    byte len = valueLengths[value];
                    if (len == currentLen)
                    {
                        _tree.Add(TreeEntry.Leaf(value));
                    }
                    else if (len > currentLen)
                    {
                        moreLeaves = true; // there are more leaves to process
                    }
                }

                // sanity check (too many leaves)
                if (_tree.Count > maxAllocated)
                {
                    throw new InvalidOperationException("Too many leaves");
                }

                // stop when no longer finding longer codes
                if (!moreLeaves)
                {
                    break;
                }
            }

            // ensure tree is complete
            if (_tree.Count != maxAllocated)
            {
                throw new InvalidOperationException(
                    $"Missing some leaves: tree count = {_tree.Count}, expected = {maxAllocated}"
                );
            }
        }

        public int ReadEntry(BitReader reader)
        {
            if (_tree.Count == 0)
            {
                throw new InvalidOperationException("Tree not initialized");
            }

            TreeEntry node = _tree[0];
            while (true)
            {
                if (node.Type == NodeType.Leaf)
                {
                    return node.LeafValue;
                }

                int bit = reader.ReadBit();
                int index = node.BranchIndex + bit;

                if (index >= _tree.Count)
                {
                    throw new InvalidOperationException("Invalid branch index during read");
                }

                node = _tree[index];
            }
        }

        public override string ToString()
        {
            var result = new StringBuilder();

            void FormatStep(int index, string prefix)
            {
                var node = _tree[index];
                if (node.Type == NodeType.Leaf)
                {
                    result.AppendLine($"{prefix} -> {node.LeafValue}");
                }
                else
                {
                    FormatStep(node.BranchIndex, prefix + "0");
                    FormatStep(node.BranchIndex + 1, prefix + "1");
                }
            }

            if (_tree.Count > 0)
            {
                FormatStep(0, "");
            }

            return result.ToString();
        }
    }
}
