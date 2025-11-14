namespace SharpCompress.Compressors.Arj
{
    public class Lh5DecoderCfg : ILhaDecoderConfig
    {
        public int HistoryBits => 14;
        public int OffsetBits => 4;
        public RingBuffer RingBuffer { get; } = new RingBuffer(1 << 14);
    }
}
