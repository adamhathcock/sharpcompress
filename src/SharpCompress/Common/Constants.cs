namespace SharpCompress.Common;

public static class Constants
{
    /// <summary>
    /// The default buffer size for stream operations, matching .NET's Stream.CopyTo default of 81920 bytes.
    /// This can be modified globally at runtime.
    /// </summary>
    public static int BufferSize = 81920;
}
