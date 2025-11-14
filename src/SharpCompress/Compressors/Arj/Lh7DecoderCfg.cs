namespace SharpCompress.Compressors.Arj
{
    public class Lh7DecoderCfg : ILhaDecoderConfig
    {
        public int HistoryBits => 17;
        public int OffsetBits => 5;
        public RingBuffer RingBuffer { get; } = new RingBuffer(1 << 17);
    }
}
