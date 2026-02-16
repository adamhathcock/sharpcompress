using System;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Compressors.Arj;

public sealed partial class HuffTree
{
    public async ValueTask<int> ReadEntryAsync(
        BitReader reader,
        CancellationToken cancellationToken
    )
    {
        if (_tree.Count == 0)
        {
            throw new ArchiveOperationException("Tree not initialized");
        }

        TreeEntry node = _tree[0];
        while (true)
        {
            if (node.Type == NodeType.Leaf)
            {
                return node.LeafValue;
            }

            int bit = await reader.ReadBitAsync(cancellationToken).ConfigureAwait(false);
            int index = node.BranchIndex + bit;

            if (index >= _tree.Count)
            {
                throw new ArchiveOperationException("Invalid branch index during read");
            }

            node = _tree[index];
        }
    }
}
