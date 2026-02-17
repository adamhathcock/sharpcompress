using SharpCompress.Common;

namespace SharpCompress.Archives.GZip;

/// <summary>
/// GZip archive supporting random access and entry stream extraction.
/// </summary>
public interface IGZipArchive : IExtractableArchive { }

/// <summary>
/// Async GZip archive supporting random access and entry stream extraction.
/// </summary>
public interface IGZipAsyncArchive : IExtractableAsyncArchive { }
