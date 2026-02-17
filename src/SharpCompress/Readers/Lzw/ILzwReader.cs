using SharpCompress.Readers;

namespace SharpCompress.Readers.Lzw;

/// <summary>
/// Reader for LZW archives.
/// </summary>
public interface ILzwReader : IReader { }

/// <summary>
/// Async reader for LZW archives.
/// </summary>
public interface ILzwAsyncReader : IAsyncReader { }
