using SharpCompress.Readers;

namespace SharpCompress.Archives.SevenZip;

/// <summary>
/// Reader for 7Zip archives - supports sequential extraction only.
/// </summary>
public interface ISevenZipReader : IReader { }

/// <summary>
/// Async reader for 7Zip archives - supports sequential extraction only.
/// </summary>
public interface ISevenZipAsyncReader : IAsyncReader { }
