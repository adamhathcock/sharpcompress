using SharpCompress.Readers;

namespace SharpCompress.Readers.Rar;

/// <summary>
/// Reader for RAR archives.
/// </summary>
public interface IRarReader : IReader { }

/// <summary>
/// Async reader for RAR archives.
/// </summary>
public interface IRarAsyncReader : IAsyncReader { }
