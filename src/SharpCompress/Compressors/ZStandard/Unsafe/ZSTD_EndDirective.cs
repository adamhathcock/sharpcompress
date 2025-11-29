namespace SharpCompress.Compressors.ZStandard.Unsafe;

/*===== Streaming compression functions =====*/
public enum ZSTD_EndDirective
{
    /// <summary>collect more data, encoder decides when to output compressed result, for optimal compression ratio</summary>
    ZSTD_e_continue = 0,

    /// <summary>
    /// Flush any data provided so far, creates (at least) one new block that can be decoded immediately on reception;
    /// frame will continue: any future data can still reference previously compressed data, improving compression.
    /// Note: multithreaded compression will block to flush as much output as possible.
    /// </summary>
    ZSTD_e_flush = 1,

    /// <summary>
    /// Flush any remaining data and close current frame.
    /// Note that frame is only closed after compressed data is fully flushed (return value == 0).
    /// After that point, any additional data starts a new frame.
    /// Note: each frame is independent (does not reference any content from previous frame).
    /// Note: multithreaded compression will block to flush as much output as possible.
    /// </summary>
    ZSTD_e_end = 2,
}
