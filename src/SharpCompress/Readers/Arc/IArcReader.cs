using SharpCompress.Readers;

namespace SharpCompress.Readers.Arc;

/// <summary>
/// Reader for ARC archives.
/// </summary>
public interface IArcReader : IReader { }

/// <summary>
/// Async reader for ARC archives.
/// </summary>
public interface IArcAsyncReader : IAsyncReader { }
