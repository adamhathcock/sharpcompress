using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Factories;
using SharpCompress.IO;
using SharpCompress.Providers;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    /// <summary>
    /// Returns information about the archive at the given file path asynchronously,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        string filePath,
        CancellationToken cancellationToken = default
    ) =>
        await GetArchiveInformationAsync(filePath, ReaderOptions.ForFilePath, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Returns information about the archive at the given file path asynchronously,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        string filePath,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return await GetArchiveInformationAsync(
                stream,
                readerOptions ?? ReaderOptions.ForFilePath,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Returns information about the archive in the given stream asynchronously,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    ) =>
        await GetArchiveInformationAsync(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Returns information about the archive in the given stream asynchronously,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async ValueTask<ArchiveInformation?> GetArchiveInformationAsync(
        Stream stream,
        ReaderOptions? readerOptions,
        CancellationToken cancellationToken = default
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = await TryFindFactoryAsync(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                cancellationToken
            )
            .ConfigureAwait(false);
        return factory is null
            ? null
            : BuildArchiveInformation(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                factory
            );
    }

    internal static ValueTask<T> FindFactoryAsync<T>(
        string filePath,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        return FindFactoryAsync<T>(new FileInfo(filePath), cancellationToken);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        fileInfo.NotNull(nameof(fileInfo));
        using Stream stream = fileInfo.OpenRead();
        return await FindFactoryAsync<T>(stream, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<T> FindFactoryAsync<T>(
        FileInfo fileInfo,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
        where T : IFactory
    {
        fileInfo.NotNull(nameof(fileInfo));
        using Stream stream = fileInfo.OpenRead();
        return await FindFactoryAsync<T>(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default
    )
        where T : IFactory
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        // Use the shared async detection loop over all factories. If the matched factory
        // implements T we return it; otherwise (or if nothing matched) we fall through
        // to the same "unsupported format" exception that the original code produced,
        // listing the T-typed factories as the hint for the caller.
        return await FindFactoryAsync<T>(stream, ReaderOptions.ForExternalStream, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<T> FindFactoryAsync<T>(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
        where T : IFactory
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = await TryFindFactoryAsync(stream, readerOptions, cancellationToken)
            .ConfigureAwait(false);
        if (factory is T typedFactory)
        {
            return typedFactory;
        }

        var extensions = string.Join(", ", Factory.Factories.OfType<T>().Select(item => item.Name));

        throw new ArchiveOperationException(
            $"Cannot determine compressed stream type. Supported Archive Formats: {extensions}"
        );
    }

    /// <summary>
    /// Async counterpart of the synchronous factory detection path.
    /// Iterates all registered factories and returns the first one whose
    /// <see cref="IFactory.IsArchiveAsync"/> recognises the stream, or <see langword="null"/>.
    /// Stream position is restored to its value at entry on both success and failure.
    /// </summary>
    private static async ValueTask<IFactory?> TryFindFactoryAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    )
    {
        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories)
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
            var isArchive = await factory
                .IsArchiveAsync(stream, readerOptions, cancellationToken)
                .ConfigureAwait(false);

            if (isArchive)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                if (
                    await IsCompressedTarAsync(stream, factory, readerOptions, cancellationToken)
                        .ConfigureAwait(false)
                )
                {
                    continue;
                }

                stream.Seek(startPosition, SeekOrigin.Begin);
                return factory;
            }
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        return null;
    }

    /// <summary>
    /// Returns information about the archive at the given file path,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    public static ArchiveInformation? GetArchiveInformation(string filePath) =>
        GetArchiveInformation(filePath, ReaderOptions.ForFilePath);

    /// <summary>
    /// Returns information about the archive at the given file path,
    /// or <see langword="null"/> if the file is not a recognized archive.
    /// </summary>
    /// <param name="filePath">Path to the archive file.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    public static ArchiveInformation? GetArchiveInformation(
        string filePath,
        ReaderOptions? readerOptions
    )
    {
        filePath.NotNullOrEmpty(nameof(filePath));
        using Stream stream = File.OpenRead(filePath);
        return GetArchiveInformation(stream, readerOptions ?? ReaderOptions.ForFilePath);
    }

    /// <summary>
    /// Returns information about the archive in the given stream,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    public static ArchiveInformation? GetArchiveInformation(Stream stream) =>
        GetArchiveInformation(stream, ReaderOptions.ForExternalStream);

    /// <summary>
    /// Returns information about the archive in the given stream,
    /// or <see langword="null"/> if the stream is not a recognized archive.
    /// </summary>
    /// <param name="stream">A readable and seekable stream positioned at the start of the archive.</param>
    /// <param name="readerOptions">Options controlling archive detection.</param>
    public static ArchiveInformation? GetArchiveInformation(
        Stream stream,
        ReaderOptions? readerOptions
    )
    {
        stream.RequireReadable();
        stream.RequireSeekable();

        var factory = TryFindFactory(stream, readerOptions ?? ReaderOptions.ForExternalStream);
        return factory is null
            ? null
            : BuildArchiveInformation(
                stream,
                readerOptions ?? ReaderOptions.ForExternalStream,
                factory
            );
    }

    /// <summary>
    /// Iterates all registered factories and returns the first one whose
    /// <see cref="IFactory.IsArchive"/> recognises the stream, or <see langword="null"/>.
    /// Stream position is restored to its value at entry on both success and failure.
    /// </summary>
    /// <remarks>
    /// This is the shared, seekable-stream detection core used by
    /// <see cref="FindFactory{T}(Stream)"/>, <see cref="IsArchive(Stream, out ArchiveType?)"/>,
    /// and <see cref="GetArchiveInformation(Stream)"/>.
    /// <para>
    /// <see cref="ReaderFactory.OpenReader(Stream, ReaderOptions)"/> uses a separate code path
    /// based on <see cref="IO.SharpCompressStream"/> rewindable buffering, which supports
    /// non-seekable streams and is therefore not unified with this helper.
    /// </para>
    /// </remarks>
    private static IFactory? TryFindFactory(Stream stream) =>
        TryFindFactory(stream, ReaderOptions.ForExternalStream);

    private static IFactory? TryFindFactory(Stream stream, ReaderOptions readerOptions)
    {
        var startPosition = stream.Position;

        foreach (var factory in Factory.Factories)
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
            var isArchive = factory.IsArchive(stream, readerOptions);

            if (isArchive)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                if (IsCompressedTar(stream, factory, readerOptions))
                {
                    continue;
                }

                stream.Seek(startPosition, SeekOrigin.Begin);
                return factory;
            }
        }

        stream.Seek(startPosition, SeekOrigin.Begin);
        return null;
    }

    private static bool IsCompressedTar(
        Stream stream,
        IFactory factory,
        ReaderOptions readerOptions
    ) =>
        GetCompressedTarType(factory) is { } compressionType
        && IsCompressedTar(stream, readerOptions, compressionType);

    private static bool IsCompressedTar(
        Stream stream,
        ReaderOptions readerOptions,
        CompressionType compressionType
    )
    {
        using var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        var testStream =
            compressionType == CompressionType.GZip
                ? readerOptions.Providers.CreateDecompressStream(
                    compressionType,
                    nonDisposingStream,
                    CompressionContext
                        .FromStream(nonDisposingStream)
                        .WithReaderOptions(readerOptions)
                )
                : readerOptions.Providers.CreateDecompressStream(
                    compressionType,
                    nonDisposingStream
                );

        try
        {
            return TarArchive.IsTarFile(testStream);
        }
        finally
        {
            DisposeProbeStream(testStream);
        }
    }

    private static async ValueTask<bool> IsCompressedTarAsync(
        Stream stream,
        IFactory factory,
        ReaderOptions readerOptions,
        CancellationToken cancellationToken
    ) =>
        GetCompressedTarType(factory) is { } compressionType
        && await IsCompressedTarAsync(stream, readerOptions, compressionType, cancellationToken)
            .ConfigureAwait(false);

    private static async ValueTask<bool> IsCompressedTarAsync(
        Stream stream,
        ReaderOptions readerOptions,
        CompressionType compressionType,
        CancellationToken cancellationToken
    )
    {
        using var nonDisposingStream = SharpCompressStream.CreateNonDisposing(stream);
        var testStream =
            compressionType == CompressionType.GZip
                ? await readerOptions
                    .Providers.CreateDecompressStreamAsync(
                        compressionType,
                        nonDisposingStream,
                        CompressionContext
                            .FromStream(nonDisposingStream)
                            .WithReaderOptions(readerOptions),
                        cancellationToken
                    )
                    .ConfigureAwait(false)
                : await readerOptions
                    .Providers.CreateDecompressStreamAsync(
                        compressionType,
                        nonDisposingStream,
                        cancellationToken
                    )
                    .ConfigureAwait(false);

        try
        {
            return await TarArchive
                .IsTarFileAsync(testStream, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            DisposeProbeStream(testStream);
        }
    }

    private static void DisposeProbeStream(Stream stream)
    {
        try
        {
#pragma warning disable VSTHRD103 // Probe streams may validate unread trailers during disposal.
            stream.Dispose();
#pragma warning restore VSTHRD103
        }
        catch
        {
            // Probes intentionally read only enough data to identify tar content.
        }
    }

    private static CompressionType? GetCompressedTarType(IFactory factory) =>
        factory switch
        {
            GZipFactory => CompressionType.GZip,
            LzwFactory => CompressionType.Lzw,
            _ => null,
        };

    private static ArchiveInformation BuildArchiveInformation(
        Stream stream,
        ReaderOptions readerOptions,
        IFactory factory
    )
    {
        var info = new ArchiveInformation(factory.KnownArchiveType, factory is IArchiveFactory);
        var startPosition = stream.Position;

        try
        {
            var probeReaderOptions = readerOptions with { LeaveStreamOpen = true };
            switch (factory)
            {
                case ZipFactory:
                    TryPopulateZipDetails(info, stream, probeReaderOptions);
                    break;
                case SevenZipFactory:
                    TryPopulateSevenZipDetails(info, stream, probeReaderOptions);
                    break;
                case RarFactory:
                    TryPopulateRarDetails(info, stream, probeReaderOptions);
                    break;
            }
        }
        finally
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }

        return info;
    }

    private static void TryPopulateZipDetails(
        ArchiveInformation info,
        Stream stream,
        ReaderOptions readerOptions
    )
    {
        try
        {
            info.ZipDataDescriptorEntryCount = GetZipDataDescriptorEntryCount(
                stream,
                readerOptions
            );
        }
        catch
        {
            // Keep archive detection resilient even when format-specific probing fails.
        }
    }

    private static void TryPopulateSevenZipDetails(
        ArchiveInformation info,
        Stream stream,
        ReaderOptions readerOptions
    )
    {
        try
        {
            info.SolidStreamCount = GetSevenZipSolidStreamCount(stream, readerOptions);
        }
        catch
        {
            // Keep archive detection resilient even when format-specific probing fails.
        }
    }

    private static void TryPopulateRarDetails(
        ArchiveInformation info,
        Stream stream,
        ReaderOptions readerOptions
    )
    {
        try
        {
            info.SolidStreamCount = GetRarSolidStreamCount(stream, readerOptions);
        }
        catch
        {
            // Keep archive detection resilient even when format-specific probing fails.
        }
    }

    private static int GetZipDataDescriptorEntryCount(Stream stream, ReaderOptions readerOptions)
    {
        using var archive = ZipArchive.OpenArchive(stream, readerOptions);
        return archive
            .Entries.OfType<ZipArchiveEntry>()
            .SelectMany(entry => entry.Parts.OfType<ZipFilePart>())
            .Count(part =>
                FlagUtility.HasFlag(part.Header.Flags, HeaderFlags.UsePostDataDescriptor)
            );
    }

    private static int GetSevenZipSolidStreamCount(Stream stream, ReaderOptions readerOptions)
    {
        using var archive = SevenZipArchive.OpenArchive(stream, readerOptions);
        return archive
            .Entries.OfType<SevenZipArchiveEntry>()
            .Where(entry => !entry.IsDirectory && entry.FilePart.Folder is not null)
            .GroupBy(entry => entry.FilePart.Folder)
            .Count(group => group.Skip(1).Any());
    }

    private static int GetRarSolidStreamCount(Stream stream, ReaderOptions readerOptions)
    {
        using var archive = RarArchive.OpenArchive(stream, readerOptions);
        return archive.IsSolid ? 1 : 0;
    }
}
