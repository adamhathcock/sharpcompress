using SharpCompress.Writers;

namespace SharpCompress.Writers.GZip;

/// <summary>
/// Writer for GZip archives.
/// </summary>
public interface IGZipWriter : IWriter { }

/// <summary>
/// Async writer for GZip archives.
/// </summary>
public interface IGZipAsyncWriter : IAsyncWriter { }
