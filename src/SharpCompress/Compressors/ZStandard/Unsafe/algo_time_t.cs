namespace SharpCompress.Compressors.ZStandard.Unsafe;

public struct algo_time_t
{
    public uint tableTime;
    public uint decode256Time;

    public algo_time_t(uint tableTime, uint decode256Time)
    {
        this.tableTime = tableTime;
        this.decode256Time = decode256Time;
    }
}
