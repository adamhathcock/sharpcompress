using SharpCompress.Common;
using SharpCompress.Writers.GZip;

namespace SharpCompress.Archives.GZip;

/// <summary>
/// GZip archive supporting random access and entry stream extraction.
/// </summary>
public interface IGZipArchive : IExtractableArchive { }

/// <summary>
/// Writable GZip archive with GZip-specific writer options.
/// </summary>
public interface IGZipWritableArchive : IGZipArchive, IWritableArchive<GZipWriterOptions> { }

/// <summary>
/// Async GZip archive supporting random access and entry stream extraction.
/// </summary>
public interface IGZipAsyncArchive : IExtractableAsyncArchive { }

/// <summary>
/// Async writable GZip archive with GZip-specific writer options.
/// </summary>
public interface IGZipWritableAsyncArchive
    : IGZipAsyncArchive,
        IWritableAsyncArchive<GZipWriterOptions> { }
