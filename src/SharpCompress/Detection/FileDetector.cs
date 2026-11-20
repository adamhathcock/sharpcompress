using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Detection;

/// <summary>Identifies archives and self-identifying standalone compression streams.</summary>
public static partial class FileDetector
{
    /// <summary>Identify a file without enumerating archive entries.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<FileDetection?> DetectFileAsync(
        string filePath,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        using var stream = File.OpenRead(filePath);
        return await DetectFileAsync(stream, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Identify a readable, seekable stream, leaving it open at its original position.</summary>
    [Zomp.SyncMethodGenerator.CreateSyncVersion]
    public static async ValueTask<FileDetection?> DetectFileAsync(
        Stream stream,
        ReaderOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();
        var position = stream.Position;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var archive = await ArchiveDetector
                .DetectArchiveAsync(stream, options, cancellationToken)
                .ConfigureAwait(false);
            if (archive is not null)
            {
                return new FileDetection(archive.FormatName, archive, archive.OuterCompressionType);
            }
            foreach (var wrapper in TarWrapper.Wrappers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (wrapper.CompressionType == CompressionType.None)
                {
                    continue;
                }
                stream.Position = position;
                if (await wrapper.IsMatchAsync(stream, cancellationToken).ConfigureAwait(false))
                {
                    return new FileDetection(
                        wrapper.CompressionType.ToString(),
                        null,
                        wrapper.CompressionType
                    );
                }
            }
            return null;
        }
        finally
        {
            stream.Position = position;
        }
    }
}

/// <summary>A recognized archive or standalone compression stream; recognition is not verification.</summary>
public sealed class FileDetection
{
    internal FileDetection(
        string formatName,
        ArchiveDetection? archive,
        CompressionType? compression
    )
    {
        FormatName = formatName;
        Archive = archive;
        Compression = compression;
    }

    public string FormatName { get; }
    public ArchiveDetection? Archive { get; }
    public CompressionType? Compression { get; }
    public bool IsStandaloneCompression => Archive is null;
}
