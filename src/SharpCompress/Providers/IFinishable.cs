namespace SharpCompress.Providers;

/// <summary>
/// Interface for compression streams that require explicit finalization
/// before disposal to ensure all compressed data is flushed properly.
/// </summary>
/// <remarks>
/// Some compression formats (notably BZip2 and LZip) require explicit
/// finalization to write trailer/footer data. Implementing this interface
/// allows generic code to handle finalization without knowing the specific stream type.
/// </remarks>
public interface IFinishable
{
    /// <summary>
    /// Finalizes the compression, flushing any remaining buffered data
    /// and writing format-specific trailer/footer bytes.
    /// </summary>
    void Finish();
}
