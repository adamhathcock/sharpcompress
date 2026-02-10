using System;
using SharpCompress.Common;
using SharpCompress.Common.Options;
using SharpCompress.Compressors;

namespace SharpCompress.Readers;

/// <summary>
/// Extension methods for fluent configuration of reader options.
/// </summary>
public static class ReaderOptionsExtensions
{
    /// <summary>
    /// Creates a copy with the specified LeaveStreamOpen value.
    /// </summary>
    public static ReaderOptions WithLeaveStreamOpen(
        this ReaderOptions options,
        bool leaveStreamOpen
    ) => options with { LeaveStreamOpen = leaveStreamOpen };

    /// <summary>
    /// Creates a copy with the specified password.
    /// </summary>
    public static ReaderOptions WithPassword(this ReaderOptions options, string? password) =>
        options with
        {
            Password = password,
        };

    /// <summary>
    /// Creates a copy with the specified archive encoding.
    /// </summary>
    public static ReaderOptions WithArchiveEncoding(
        this ReaderOptions options,
        IArchiveEncoding encoding
    ) => options with { ArchiveEncoding = encoding };

    /// <summary>
    /// Creates a copy with the specified LookForHeader value.
    /// </summary>
    public static ReaderOptions WithLookForHeader(this ReaderOptions options, bool lookForHeader) =>
        options with
        {
            LookForHeader = lookForHeader,
        };

    /// <summary>
    /// Creates a copy with the specified DisableCheckIncomplete value.
    /// </summary>
    public static ReaderOptions WithDisableCheckIncomplete(
        this ReaderOptions options,
        bool disableCheckIncomplete
    ) => options with { DisableCheckIncomplete = disableCheckIncomplete };

    /// <summary>
    /// Creates a copy with the specified buffer size.
    /// </summary>
    public static ReaderOptions WithBufferSize(this ReaderOptions options, int bufferSize) =>
        options with
        {
            BufferSize = bufferSize,
        };

    /// <summary>
    /// Creates a copy with the specified extension hint.
    /// </summary>
    public static ReaderOptions WithExtensionHint(
        this ReaderOptions options,
        string? extensionHint
    ) => options with { ExtensionHint = extensionHint };

    /// <summary>
    /// Creates a copy with the specified progress reporter.
    /// </summary>
    public static ReaderOptions WithProgress(
        this ReaderOptions options,
        IProgress<ProgressReport>? progress
    ) => options with { Progress = progress };

    /// <summary>
    /// Creates a copy with the specified rewindable buffer size.
    /// </summary>
    public static ReaderOptions WithRewindableBufferSize(
        this ReaderOptions options,
        int? rewindableBufferSize
    ) => options with { RewindableBufferSize = rewindableBufferSize };

    /// <summary>
    /// Creates a copy with the specified overwrite setting.
    /// </summary>
    public static ReaderOptions WithOverwrite(this ReaderOptions options, bool overwrite) =>
        options with
        {
            Overwrite = overwrite,
        };

    /// <summary>
    /// Creates a copy with the specified extract full path setting.
    /// </summary>
    public static ReaderOptions WithExtractFullPath(
        this ReaderOptions options,
        bool extractFullPath
    ) => options with { ExtractFullPath = extractFullPath };

    /// <summary>
    /// Creates a copy with the specified preserve file time setting.
    /// </summary>
    public static ReaderOptions WithPreserveFileTime(
        this ReaderOptions options,
        bool preserveFileTime
    ) => options with { PreserveFileTime = preserveFileTime };

    /// <summary>
    /// Creates a copy with the specified preserve attributes setting.
    /// </summary>
    public static ReaderOptions WithPreserveAttributes(
        this ReaderOptions options,
        bool preserveAttributes
    ) => options with { PreserveAttributes = preserveAttributes };

    /// <summary>
    /// Creates a copy with the specified symbolic link handler.
    /// </summary>
    public static ReaderOptions WithSymbolicLinkHandler(
        this ReaderOptions options,
        Action<string, string>? handler
    ) => options with { SymbolicLinkHandler = handler };

    /// <summary>
    /// Creates a copy with the specified compression provider registry.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="providers"/> is null.</exception>
    public static ReaderOptions WithProviders(
        this ReaderOptions options,
        CompressionProviderRegistry providers
    )
    {
        _ = providers ?? throw new ArgumentNullException(nameof(providers));
        return options with { Providers = providers };
    }
}
