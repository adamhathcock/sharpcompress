using SharpCompress.Common;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Archives.Tar;

/// <summary>
/// TAR archive supporting random access and entry stream extraction.
/// </summary>
public interface ITarArchive : IExtractableArchive { }

/// <summary>
/// Writable TAR archive with TAR-specific writer options.
/// </summary>
public interface ITarWritableArchive : ITarArchive, IWritableArchive<TarWriterOptions> { }

/// <summary>
/// Async TAR archive supporting random access and entry stream extraction.
/// </summary>
public interface ITarAsyncArchive : IExtractableAsyncArchive { }

/// <summary>
/// Async writable TAR archive with TAR-specific writer options.
/// </summary>
public interface ITarWritableAsyncArchive
    : ITarAsyncArchive,
        IWritableAsyncArchive<TarWriterOptions> { }
