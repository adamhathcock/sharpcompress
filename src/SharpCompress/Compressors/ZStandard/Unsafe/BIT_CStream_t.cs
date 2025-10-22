namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* bitStream can mix input from multiple sources.
 * A critical property of these streams is that they encode and decode in **reverse** direction.
 * So the first bit sequence you add will be the last to be read, like a LIFO stack.
 */
public unsafe struct BIT_CStream_t
{
    public nuint bitContainer;
    public uint bitPos;
    public sbyte* startPtr;
    public sbyte* ptr;
    public sbyte* endPtr;
}
