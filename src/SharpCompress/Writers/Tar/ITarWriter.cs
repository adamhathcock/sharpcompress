using SharpCompress.Writers;

namespace SharpCompress.Writers.Tar;

/// <summary>
/// Writer for TAR archives.
/// </summary>
public interface ITarWriter : IWriter { }

/// <summary>
/// Async writer for TAR archives.
/// </summary>
public interface ITarAsyncWriter : IAsyncWriter { }
