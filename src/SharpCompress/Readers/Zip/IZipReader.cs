using SharpCompress.Readers;

namespace SharpCompress.Readers.Zip;

/// <summary>
/// Reader for ZIP archives.
/// </summary>
public interface IZipReader : IReader { }

/// <summary>
/// Async reader for ZIP archives.
/// </summary>
public interface IZipAsyncReader : IAsyncReader { }
