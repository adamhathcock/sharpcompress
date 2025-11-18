namespace SharpCompress.Compressors.Arj
{
    public interface ILhaDecoderConfig
    {
        int HistoryBits { get; }
        int OffsetBits { get; }
        RingBuffer RingBuffer { get; }
    }
}
