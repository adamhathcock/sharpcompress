using SharpCompress.Writers;

namespace SharpCompress.Writers.Zip;

/// <summary>
/// Writer for ZIP archives.
/// </summary>
public interface IZipWriter : IWriter { }

/// <summary>
/// Async writer for ZIP archives.
/// </summary>
public interface IZipAsyncWriter : IAsyncWriter { }
