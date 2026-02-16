using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.Deflate64;
using SharpCompress.Compressors.Explode;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.PPMd;
using SharpCompress.Compressors.Reduce;
using SharpCompress.Compressors.Shrink;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
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

    internal override Stream GetCompressedStream()
    {
        if (!Header.HasData)
        {
            return Stream.Null;
        }
        var decompressionStream = CreateDecompressionStream(
            GetCryptoStream(CreateBaseStream()),
            Header.CompressionMethod
        );
        if (LeaveStreamOpen)
        {
            return SharpCompressStream.CreateNonDisposing(decompressionStream);
        }
        return decompressionStream;
    }

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

    private Stream CreateWinzipAesDecompressionStream(Stream stream)
    {
        var data = Header.Extra.SingleOrDefault(x => x.Type == ExtraDataType.WinZipAes);
        if (data is null)
        {
            throw new InvalidFormatException("No Winzip AES extra data found.");
        }
        if (data.Length != 7)
        {
            throw new InvalidFormatException("Winzip data length is not 7.");
        }
        var compressedMethod = BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes);

        if (compressedMethod != 0x01 && compressedMethod != 0x02)
        {
            throw new InvalidFormatException(
                "Unexpected vendor version number for WinZip AES metadata"
            );
        }

        var vendorId = BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes.AsSpan(2));
        if (vendorId != 0x4541)
        {
            throw new InvalidFormatException("Unexpected vendor ID for WinZip AES metadata");
        }
        return CreateDecompressionStream(
            stream,
            (ZipCompressionMethod)BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes.AsSpan(5))
        );
    }

    protected Stream GetCryptoStream(Stream plainStream)
    {
        var isFileEncrypted = FlagUtility.HasFlag(Header.Flags, HeaderFlags.Encrypted);

        if (Header.CompressedSize == 0 && isFileEncrypted)
        {
            throw new NotSupportedException("Cannot encrypt file with unknown size at start.");
        }

        if (
            (
                Header.CompressedSize == 0
                && FlagUtility.HasFlag(Header.Flags, HeaderFlags.UsePostDataDescriptor)
            ) || Header.IsZip64
        )
        {
            plainStream = SharpCompressStream.CreateNonDisposing(plainStream); //make sure AES doesn't close
        }
        else
        {
            plainStream = new ReadOnlySubStream(plainStream, Header.CompressedSize); //make sure AES doesn't close
        }

        if (isFileEncrypted)
        {
            switch (Header.CompressionMethod)
            {
                case ZipCompressionMethod.None:
                case ZipCompressionMethod.Shrink:
                case ZipCompressionMethod.Reduce1:
                case ZipCompressionMethod.Reduce2:
                case ZipCompressionMethod.Reduce3:
                case ZipCompressionMethod.Reduce4:
                case ZipCompressionMethod.Deflate:
                case ZipCompressionMethod.Deflate64:
                case ZipCompressionMethod.BZip2:
                case ZipCompressionMethod.LZMA:
                case ZipCompressionMethod.PPMd:
                {
                    return new PkwareTraditionalCryptoStream(
                        plainStream,
                        Header.ComposeEncryptionData(plainStream),
                        CryptoMode.Decrypt
                    );
                }

                case ZipCompressionMethod.WinzipAes:
                {
                    if (Header.WinzipAesEncryptionData != null)
                    {
                        return new WinzipAesCryptoStream(
                            plainStream,
                            Header.WinzipAesEncryptionData,
                            Header.CompressedSize - 10
                        );
                    }
                    return plainStream;
                }

                default:
                {
                    throw new ArchiveOperationException("Header.CompressionMethod is invalid");
                }
            }
        }
        return plainStream;
    }
}
