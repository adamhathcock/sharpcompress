using SharpCompress.Common;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Archives.Zip;

/// <summary>
/// ZIP archive supporting random access and entry stream extraction.
/// </summary>
public interface IZipArchive : IExtractableArchive { }

/// <summary>
/// Writable ZIP archive with ZIP-specific writer options.
/// </summary>
public interface IZipWritableArchive : IZipArchive, IWritableArchive<ZipWriterOptions> { }

/// <summary>
/// Async ZIP archive supporting random access and entry stream extraction.
/// </summary>
public interface IZipAsyncArchive : IExtractableAsyncArchive { }

/// <summary>
/// Async writable ZIP archive with ZIP-specific writer options.
/// </summary>
public interface IZipWritableAsyncArchive
    : IZipAsyncArchive,
        IWritableAsyncArchive<ZipWriterOptions> { }
