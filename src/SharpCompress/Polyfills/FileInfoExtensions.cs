using System.IO;
using System.Threading;

namespace SharpCompress;

public static class FileInfoExtensions
{
    /// <summary>
    /// Opens a file stream for asynchronous writing.
    /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
    /// Falls back to FileStream constructor with async options on legacy frameworks.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A FileStream configured for asynchronous operations.</returns>
    public static Stream OpenAsyncWriteStream(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

#if NET8_0_OR_GREATER
        // Use File.OpenHandle with async options for .NET 8.0+
        var handle = File.OpenHandle(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            FileOptions.Asynchronous
        );
        return new FileStream(handle, FileAccess.Write);
#else
        // For older target frameworks, use FileStream constructor with async options
        return new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096, //default
            FileOptions.Asynchronous
        );
#endif
    }

    /// <summary>
    /// Opens a file stream for asynchronous reading.
    /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
    /// Falls back to FileStream constructor with async options on legacy frameworks.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A FileStream configured for asynchronous operations.</returns>
    public static Stream OpenAsyncReadStream(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

#if NET8_0_OR_GREATER
        // Use File.OpenHandle with async options for .NET 8.0+
        var handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.Asynchronous
        );
        return new FileStream(handle, FileAccess.Read);
#else
        // For older target frameworks, use FileStream constructor with async options
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous
        );
#endif
    }

    /// <param name="fileInfo">The FileInfo to open.</param>
    extension(FileInfo fileInfo)
    {
        /// <summary>
        /// Opens a file stream for asynchronous reading from a FileInfo.
        /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
        /// Falls back to FileStream constructor with async options on legacy frameworks.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A FileStream configured for asynchronous operations.</returns>
        public Stream OpenAsyncReadStream(CancellationToken cancellationToken)
        {
            fileInfo.NotNull(nameof(fileInfo));
            return OpenAsyncReadStream(fileInfo.FullName, cancellationToken);
        }

        /// <summary>
        /// Opens a file stream for asynchronous writing from a FileInfo.
        /// Uses File.OpenHandle with FileOptions.Asynchronous on .NET 8.0+ for optimal performance.
        /// Falls back to FileStream constructor with async options on legacy frameworks.
        /// </summary>
        /// <param name="fileInfo">The FileInfo to open.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A FileStream configured for asynchronous operations.</returns>
        public Stream OpenAsyncWriteStream(CancellationToken cancellationToken)
        {
            fileInfo.NotNull(nameof(fileInfo));
            return OpenAsyncWriteStream(fileInfo.FullName, cancellationToken);
        }
    }
}
