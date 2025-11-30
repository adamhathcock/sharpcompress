using System;
using SharpCompress.Common;

namespace SharpCompress.Archives;

/// <summary>
/// Internal interface for archives that support progress reporting.
/// </summary>
internal interface IArchiveProgressInfo
{
    /// <summary>
    /// Gets the progress reporter for this archive, if one was set.
    /// </summary>
    IProgress<ProgressReport>? Progress { get; }

    /// <summary>
    /// Ensures all entries are loaded from the archive.
    /// </summary>
    void EnsureEntriesLoaded();
}
