namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct BlockSummary
{
    public nuint nbSequences;
    public nuint blockSize;
    public nuint litSize;
}
