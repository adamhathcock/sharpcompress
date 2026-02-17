using SharpCompress.Common;

namespace SharpCompress.Archives.SevenZip;

/// <summary>
/// 7Zip archive supporting metadata access and sequential extraction only.
/// Does NOT support IExtractableArchive because 7Zip requires sequential decompression.
/// </summary>
public interface ISevenZipArchive : IArchive { }

/// <summary>
/// Async 7Zip archive supporting metadata access and sequential extraction only.
/// Does NOT support IExtractableAsyncArchive because 7Zip requires sequential decompression.
/// </summary>
public interface ISevenZipAsyncArchive : IAsyncArchive { }
