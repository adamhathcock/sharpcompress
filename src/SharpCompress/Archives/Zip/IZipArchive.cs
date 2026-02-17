using SharpCompress.Common;

namespace SharpCompress.Archives.Zip;

/// <summary>
/// ZIP archive supporting random access and entry stream extraction.
/// </summary>
public interface IZipArchive : IExtractableArchive { }

/// <summary>
/// Async ZIP archive supporting random access and entry stream extraction.
/// </summary>
public interface IZipAsyncArchive : IExtractableAsyncArchive { }
