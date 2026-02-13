using System.Text;
using SharpCompress.Common;
using SharpCompress.Common.Options;

namespace SharpCompress.Providers;

public static class CompressionContextExtensions
{
    /// <summary>
    /// Resolves the archive header encoding from <see cref="CompressionContext.ReaderOptions"/>.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="ReaderOptions.ArchiveEncoding"/> when ReaderOptions is set,
    /// otherwise falls back to UTF-8.
    /// </remarks>
    public static Encoding ResolveArchiveEncoding(this CompressionContext context) =>
        context.ReaderOptions?.ArchiveEncoding.GetEncoding() ?? Encoding.UTF8;
}
