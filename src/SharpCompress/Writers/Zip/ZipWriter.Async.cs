using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;

namespace SharpCompress.Writers.Zip;

public partial class ZipWriter
{
    /// <summary>
    /// Asynchronously writes a directory entry to the ZIP archive.
    /// Uses synchronous implementation for directory entries as they are lightweight.
    /// </summary>
    public override async ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        // Synchronous implementation is sufficient for directory entries
        WriteDirectory(directoryName, modificationTime);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
