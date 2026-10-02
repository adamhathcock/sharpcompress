using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>
/// Extracts content with checksum, progress, path, and metadata handling.
/// Supplied archives, readers, and destination streams remain caller-owned.
/// Reader extraction consumes entries from the reader's current position.
/// </summary>
public interface IArchiveExtractor
{
    void ExtractToDirectory(
        IArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null
    );
    ValueTask ExtractToDirectoryAsync(
        IAsyncArchive archive,
        string destinationDirectory,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
    void ExtractToDirectory(
        IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    );
    ValueTask ExtractToDirectoryAsync(
        IAsyncReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    );
    void ExtractEntryToDirectory(
        IReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null
    );
    ValueTask ExtractEntryToDirectoryAsync(
        IAsyncReader reader,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    );
    void ExtractToDirectory(
        IArchiveEntry entry,
        string destinationDirectory,
        ExtractionOptions? options = null
    );
    ValueTask ExtractToDirectoryAsync(
        IArchiveEntry entry,
        string destinationDirectory,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    );
    void ExtractToFile(
        IArchiveEntry entry,
        string destinationFileName,
        ExtractionOptions? options = null
    );
    ValueTask ExtractToFileAsync(
        IArchiveEntry entry,
        string destinationFileName,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    );
    void ExtractToFile(
        IReader reader,
        string destinationFileName,
        ExtractionOptions? options = null
    );
    ValueTask ExtractToFileAsync(
        IAsyncReader reader,
        string destinationFileName,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    );
    void ExtractToStream(
        IArchiveEntry entry,
        Stream destination,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null
    );
    ValueTask ExtractToStreamAsync(
        IArchiveEntry entry,
        Stream destination,
        ExtractionOptions? options = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancellationToken = default
    );
    void ExtractToStream(IReader reader, Stream destination, ExtractionOptions? options = null);
    ValueTask ExtractToStreamAsync(
        IAsyncReader reader,
        Stream destination,
        ExtractionOptions? options = null,
        CancellationToken cancellationToken = default
    );
}
