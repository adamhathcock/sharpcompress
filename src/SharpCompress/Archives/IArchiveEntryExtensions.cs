using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;

namespace SharpCompress.Archives;

/// <summary>Convenience entry extraction using the default extraction service.</summary>
public static partial class IArchiveEntryExtensions
{
    extension(IArchiveEntry entry)
    {
        /// <summary>Extract entry to the specified stream.</summary>
        [Zomp.SyncMethodGenerator.CreateSyncVersion(PreserveProgress = true)]
        public ValueTask WriteToAsync(
            Stream streamToWriteTo,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToStreamAsync(
                entry,
                streamToWriteTo,
                progress: progress,
                cancellationToken: cancellationToken
            );

        /// <summary>Extract entry to the specified stream.</summary>
        [Zomp.SyncMethodGenerator.CreateSyncVersion(PreserveProgress = true)]
        public ValueTask WriteToAsync(
            Stream streamToWriteTo,
            ExtractionOptions options,
            IProgress<ProgressReport>? progress = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToStreamAsync(
                entry,
                streamToWriteTo,
                options,
                progress,
                cancellationToken
            );

        /// <summary>Extract entry to the specified directory.</summary>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public ValueTask WriteToDirectoryAsync(
            string destinationDirectory,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToDirectoryAsync(
                entry,
                destinationDirectory,
                options,
                cancellationToken
            );

        /// <summary>Extract entry to the specified file.</summary>
        [Zomp.SyncMethodGenerator.CreateSyncVersion]
        public ValueTask WriteToFileAsync(
            string destinationFileName,
            ExtractionOptions? options = null,
            CancellationToken cancellationToken = default
        ) =>
            ClientDefaults.Client.Extractor.ExtractToFileAsync(
                entry,
                destinationFileName,
                options,
                cancellationToken
            );
    }
}
