using System.IO;
using SharpCompress.Common;

namespace SharpCompress.Compressors;

/// <summary>
/// Extended compression provider interface for formats that require initialization/finalization data.
/// </summary>
/// <remarks>
/// Some compression formats (like LZMA and PPMd in Zip) require special handling:
/// - Data written before compression starts (magic bytes, properties headers)
/// - Data written after compression completes (properties, footers)
/// This interface extends ICompressionProvider to support these complex initialization patterns
/// while keeping the simple ICompressionProvider interface for formats that don't need it.
/// </remarks>
public interface ICompressingProvider : ICompressionProvider
{
    /// <summary>
    /// Gets initialization data to write before compression starts.
    /// Returns null if no pre-compression data is needed.
    /// </summary>
    /// <param name="context">Context information.</param>
    /// <returns>Bytes to write before compression, or null.</returns>
    byte[]? GetPreCompressionData(CompressionContext context);

    /// <summary>
    /// Gets properties/data to write after creating the compression stream but before writing data.
    /// Returns null if no properties are needed.
    /// </summary>
    /// <param name="stream">The compression stream that was created.</param>
    /// <param name="context">Context information.</param>
    /// <returns>Bytes to write after stream creation, or null.</returns>
    byte[]? GetCompressionProperties(Stream stream, CompressionContext context);

    /// <summary>
    /// Gets data to write after compression is complete.
    /// Returns null if no post-compression data is needed.
    /// </summary>
    /// <param name="stream">The compression stream.</param>
    /// <param name="context">Context information.</param>
    /// <returns>Bytes to write after compression, or null.</returns>
    byte[]? GetPostCompressionData(Stream stream, CompressionContext context);
}
