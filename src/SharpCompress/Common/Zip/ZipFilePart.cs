using System;
using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Common.Zip;

internal abstract partial class ZipFilePart : FilePart
{
    private readonly CompressionProviderRegistry _compressionProviders;

    internal ZipFilePart(
        ZipFileEntry header,
        Stream stream,
        CompressionProviderRegistry compressionProviders
    )
        : base(header.ArchiveEncoding)
    {
        Header = header;
        header.Part = this;
        BaseStream = stream;
        _compressionProviders = compressionProviders;
    }

    internal Stream BaseStream { get; }
    internal ZipFileEntry Header { get; set; }

    internal override string? FilePartName => Header.Name;

    internal override Stream GetRawStream()
    {
        if (!Header.HasData)
        {
            return Stream.Null;
        }
        return CreateBaseStream();
    }

    protected abstract Stream CreateBaseStream();

    protected bool LeaveStreamOpen =>
        FlagUtility.HasFlag(Header.Flags, HeaderFlags.UsePostDataDescriptor) || Header.IsZip64;

    /// <summary>
    /// Gets the compression provider registry, falling back to default if not set.
    /// </summary>
    protected CompressionProviderRegistry GetProviders() => _compressionProviders;

    /// <summary>
    /// Converts ZipCompressionMethod to CompressionType.
    /// </summary>
    protected static CompressionType ToCompressionType(ZipCompressionMethod method) =>
        method switch
        {
            ZipCompressionMethod.None => CompressionType.None,
            ZipCompressionMethod.Deflate => CompressionType.Deflate,
            ZipCompressionMethod.Deflate64 => CompressionType.Deflate64,
            ZipCompressionMethod.BZip2 => CompressionType.BZip2,
            ZipCompressionMethod.LZMA => CompressionType.LZMA,
            ZipCompressionMethod.PPMd => CompressionType.PPMd,
            ZipCompressionMethod.ZStandard => CompressionType.ZStandard,
            ZipCompressionMethod.Xz => CompressionType.Xz,
            ZipCompressionMethod.Shrink => CompressionType.Shrink,
            ZipCompressionMethod.Reduce1 => CompressionType.Reduce1,
            ZipCompressionMethod.Reduce2 => CompressionType.Reduce2,
            ZipCompressionMethod.Reduce3 => CompressionType.Reduce3,
            ZipCompressionMethod.Reduce4 => CompressionType.Reduce4,
            ZipCompressionMethod.Explode => CompressionType.Explode,
            _ => throw new NotSupportedException($"Unsupported compression method: {method}"),
        };

    protected Stream CreateDecompressionStream(Stream stream, ZipCompressionMethod method)
    {
        // Handle special cases first
        switch (method)
        {
            case ZipCompressionMethod.None:
            {
                if (Header.CompressedSize is 0)
                {
                    return new DataDescriptorStream(stream);
                }
                return stream;
            }
            case ZipCompressionMethod.WinzipAes:
            {
                return CreateWinzipAesDecompressionStream(stream);
            }
        }

        // Get the compression type and providers
        var compressionType = ToCompressionType(method);
        var providers = GetProviders();

        // Build context with header information
        var context = new CompressionContext
        {
            InputSize = Header.CompressedSize,
            OutputSize = Header.UncompressedSize,
            CanSeek = stream.CanSeek,
        };

        // Handle methods that need special context
        switch (method)
        {
            case ZipCompressionMethod.LZMA:
            {
                if (FlagUtility.HasFlag(Header.Flags, HeaderFlags.Encrypted))
                {
                    throw new NotSupportedException("LZMA with pkware encryption.");
                }

                using var reader = new BinaryReader(
                    stream,
                    System.Text.Encoding.Default,
                    leaveOpen: true
                );
                reader.ReadUInt16(); // LZMA version
                var propsLength = reader.ReadUInt16();
                var props = reader.ReadBytes(propsLength);

                // When the uncompressed size is known to be zero, skip remaining compressed
                // bytes (required for streaming reads) and return an empty stream.
                // Bit1 (EOS marker flag) means the output size is not stored in the header
                // (the LZMA stream itself contains an end-of-stream marker instead), so we
                // only short-circuit when the size is explicitly known to be zero.
                if (
                    !FlagUtility.HasFlag(Header.Flags, HeaderFlags.Bit1)
                    && Header.UncompressedSize == 0
                )
                {
                    stream.Skip();
                    return Stream.Null;
                }

                context = context with
                {
                    Properties = props,
                    InputSize =
                        Header.CompressedSize > 0 ? Header.CompressedSize - 4 - props.Length : -1,
                    OutputSize = FlagUtility.HasFlag(Header.Flags, HeaderFlags.Bit1)
                        ? -1
                        : Header.UncompressedSize,
                };
                return providers.CreateDecompressStream(compressionType, stream, context);
            }
            case ZipCompressionMethod.PPMd:
            {
                Span<byte> props = stackalloc byte[2];
                stream.ReadFully(props);
                context = context with { Properties = props.ToArray() };
                return providers.CreateDecompressStream(compressionType, stream, context);
            }
            case ZipCompressionMethod.Explode:
            {
                context = context with { FormatOptions = Header.Flags };
                return providers.CreateDecompressStream(compressionType, stream, context);
            }
        }

        // For simple methods, use the basic decompress
        return providers.CreateDecompressStream(compressionType, stream, context);
    }
}
