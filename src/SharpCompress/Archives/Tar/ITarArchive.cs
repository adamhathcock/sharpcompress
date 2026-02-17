using SharpCompress.Common;

namespace SharpCompress.Archives.Tar;

/// <summary>
/// TAR archive supporting random access and entry stream extraction.
/// </summary>
public interface ITarArchive : IExtractableArchive { }

/// <summary>
/// Async TAR archive supporting random access and entry stream extraction.
/// </summary>
public interface ITarAsyncArchive : IExtractableAsyncArchive { }
