using SharpCompress.Common;

namespace SharpCompress.Archives;

/// <summary>
/// Contains detected archive format details and supported capabilities.
/// </summary>
/// <param name="Type">The detected archive type.</param>
/// <param name="SupportsRandomAccess">
/// <see langword="true" /> when the detected format supports the <see cref="IArchive" /> API;
/// otherwise <see langword="false" /> when only sequential reader access is available.
/// </param>
public sealed record ArchiveFormatInfo(ArchiveType? Type, bool SupportsRandomAccess);
