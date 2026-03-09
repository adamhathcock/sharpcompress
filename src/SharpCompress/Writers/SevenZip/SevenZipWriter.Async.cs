using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.SevenZip;

namespace SharpCompress.Writers.SevenZip;

public partial class SevenZipWriter
{
    /// <summary>
    /// Asynchronously writes a file entry to the 7z archive.
    /// </summary>
    public override async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        if (finalized)
        {
            throw new ObjectDisposedException(
                nameof(SevenZipWriter),
                "Cannot write to a finalized archive."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        filename = NormalizeFilename(filename);
        var context = new SevenZipWriteContext(filename, modificationTime);
        var progressStream = WrapWithProgress(source, filename);

        var firstByteBuffer = new byte[1];
        var firstByteRead = await progressStream
            .ReadAsync(firstByteBuffer, 0, 1, cancellationToken)
            .ConfigureAwait(false);
        if (firstByteRead == 0)
        {
            FinalizeActiveFolder();
            AddEmptyFileEntry(filename, modificationTime);
            return;
        }

        var groupKey = sevenZipOptions.Solid.ResolveGroupKey(context);
        EnsureActiveFolder(groupKey);
        await activeFolderCompressor
            .NotNull()
            .AppendAsync(progressStream, firstByteBuffer[0], cancellationToken)
            .ConfigureAwait(false);

        entries.Add(
            new SevenZipWriteEntry
            {
                Name = filename,
                ModificationTime = modificationTime,
                IsDirectory = false,
                IsEmpty = false,
            }
        );

        if (groupKey is null)
        {
            FinalizeActiveFolder();
        }
    }

    /// <summary>
    /// Asynchronously writes a directory entry to the 7z archive.
    /// </summary>
    public override ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteDirectory(directoryName, modificationTime);
        return new ValueTask();
    }
}
