using SharpCompress.Common.Options;
using SharpCompress.Factories;
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.GZip;
using SharpCompress.Writers.SevenZip;
using SharpCompress.Writers.Tar;
using SharpCompress.Writers.Zip;

namespace SharpCompress;

/// <summary>Immutable dependencies shared by the services belonging to a client.</summary>
public sealed class SharpCompressConfiguration
{
    public FormatRegistry Formats { get; }
    public CompressionProviderRegistry Providers { get; }

    public SharpCompressConfiguration(
        FormatRegistry? formats = null,
        CompressionProviderRegistry? providers = null
    )
    {
        Formats = formats ?? FormatRegistry.Default;
        Providers = providers ?? CompressionProviderRegistry.Default;
    }

    internal ReaderOptions PrepareReaderOptions(ReaderOptions? options, bool externalStream)
    {
        options ??= externalStream ? ReaderOptions.ForExternalStream : ReaderOptions.ForFilePath;
        // Resolve into a copy so caller-owned options are never changed by client configuration.
        return options with
        {
            Formats = Formats,
            Providers = options.HasProviderOverride ? options.Providers : Providers,
        };
    }

    internal IWriterOptions PrepareWriterOptions(IWriterOptions options)
    {
        ThrowHelper.ThrowIfNull(options);
        // Preserve format-specific settings and explicit per-operation provider overrides.
        return options switch
        {
            WriterOptions value => value with
            {
                Providers = value.HasProviderOverride ? value.Providers : Providers,
            },
            ZipWriterOptions value => value with
            {
                Providers = value.HasProviderOverride ? value.Providers : Providers,
            },
            TarWriterOptions value => value with
            {
                Providers = value.HasProviderOverride ? value.Providers : Providers,
            },
            GZipWriterOptions value => value with
            {
                Providers = value.HasProviderOverride ? value.Providers : Providers,
            },
            SevenZipWriterOptions value => value with
            {
                Providers = value.HasProviderOverride ? value.Providers : Providers,
            },
            // Custom options belong to a custom format factory and carry their own dependencies.
            _ => options,
        };
    }

    // Option conversion must preserve the distinction between unset and explicitly selected defaults.
    internal static CompressionProviderRegistry? GetProviderOverride(IWriterOptions options) =>
        options switch
        {
            WriterOptions value => value.HasProviderOverride ? value.Providers : null,
            ZipWriterOptions value => value.HasProviderOverride ? value.Providers : null,
            TarWriterOptions value => value.HasProviderOverride ? value.Providers : null,
            GZipWriterOptions value => value.HasProviderOverride ? value.Providers : null,
            SevenZipWriterOptions value => value.HasProviderOverride ? value.Providers : null,
            _ => options.Providers,
        };
}
