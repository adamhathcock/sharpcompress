namespace SharpCompress.Compressors.ZStandard.Unsafe;

/* Streaming state is used to inform allocation of the literal buffer */
public enum streaming_operation
{
    not_streaming = 0,
    is_streaming = 1,
}
