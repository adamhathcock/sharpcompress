using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Detection;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

/// <summary>Recognizes formats and inspects metadata without taking ownership of supplied streams.</summary>
public interface IArchiveInspector
{
    ArchiveDetection? DetectArchive(string filePath, ReaderOptions? options = null);
    ArchiveDetection? DetectArchive(Stream stream, ReaderOptions? options = null);
    ValueTask<ArchiveDetection?> DetectArchiveAsync(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<ArchiveDetection?> DetectArchiveAsync(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ArchiveInformation? InspectArchive(string filePath, ReaderOptions? options = null);
    ArchiveInformation? InspectArchive(Stream stream, ReaderOptions? options = null);
    ArchiveInformation? InspectArchive(
        IReadOnlyList<FileInfo> files,
        ReaderOptions? options = null
    );
    ArchiveInformation? InspectArchive(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null
    );
    ValueTask<ArchiveInformation?> InspectArchiveAsync(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<ArchiveInformation?> InspectArchiveAsync(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<FileInfo> files,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    ValueTask<ArchiveInformation?> InspectArchiveAsync(
        IReadOnlyList<Stream> streams,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    );
    IEnumerable<string> GetFileParts(string firstPart);
    IEnumerable<FileInfo> GetFileParts(FileInfo firstPart);
}
