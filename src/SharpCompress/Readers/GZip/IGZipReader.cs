using SharpCompress.Readers;

namespace SharpCompress.Readers.GZip;

/// <summary>
/// Reader for GZip archives.
/// </summary>
public interface IGZipReader : IReader { }

/// <summary>
/// Async reader for GZip archives.
/// </summary>
public interface IGZipAsyncReader : IAsyncReader { }
