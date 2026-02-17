using SharpCompress.Readers;

namespace SharpCompress.Readers.Tar;

/// <summary>
/// Reader for TAR archives.
/// </summary>
public interface ITarReader : IReader { }

/// <summary>
/// Async reader for TAR archives.
/// </summary>
public interface ITarAsyncReader : IAsyncReader { }
