using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.IO;
using SharpCompress.Providers;

namespace SharpCompress.Common.Zip;

internal abstract partial class ZipFilePart
{
    internal override async ValueTask<Stream?> GetCompressedStreamAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!Header.HasData)
        {
            return Stream.Null;
        }
        var decompressionStream = await CreateDecompressionStreamAsync(
                await GetCryptoStreamAsync(CreateBaseStream(), cancellationToken)
                    .ConfigureAwait(false),
                Header.CompressionMethod,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (LeaveStreamOpen)
        {
            return SharpCompressStream.CreateNonDisposing(decompressionStream);
        }
        return decompressionStream;
    }

    protected async ValueTask<Stream> GetCryptoStreamAsync(
        Stream plainStream,
        CancellationToken cancellationToken = default
    )
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
                        await Header
                            .ComposeEncryptionDataAsync(plainStream, cancellationToken)
                            .ConfigureAwait(false),
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

    protected async ValueTask<Stream> CreateDecompressionStreamAsync(
        Stream stream,
        ZipCompressionMethod method,
        CancellationToken cancellationToken = default
    )
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
                return await CreateWinzipAesDecompressionStreamAsync(stream, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var compressionType = ToCompressionType(method);
        var providers = GetProviders();
        var context = new CompressionContext
        {
            InputSize = Header.CompressedSize,
            OutputSize = Header.UncompressedSize,
            CanSeek = stream.CanSeek,
        };

        switch (method)
        {
            case ZipCompressionMethod.LZMA:
            {
                if (FlagUtility.HasFlag(Header.Flags, HeaderFlags.Encrypted))
                {
                    throw new NotSupportedException("LZMA with pkware encryption.");
                }
                var buffer = new byte[4];
                await stream.ReadFullyAsync(buffer, 0, 4, cancellationToken).ConfigureAwait(false);
                var propsSize = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(2, 2));
                var props = new byte[propsSize];
                await stream
                    .ReadFullyAsync(props, 0, propsSize, cancellationToken)
                    .ConfigureAwait(false);

                context = context with
                {
                    Properties = props,
                    InputSize =
                        Header.CompressedSize > 0 ? Header.CompressedSize - 4 - props.Length : -1,
                    OutputSize = FlagUtility.HasFlag(Header.Flags, HeaderFlags.Bit1)
                        ? -1
                        : Header.UncompressedSize,
                };

                return await providers
                    .CreateDecompressStreamAsync(
                        compressionType,
                        stream,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            case ZipCompressionMethod.PPMd:
            {
                var props = new byte[2];
                await stream.ReadFullyAsync(props, 0, 2, cancellationToken).ConfigureAwait(false);
                context = context with { Properties = props };
                return await providers
                    .CreateDecompressStreamAsync(
                        compressionType,
                        stream,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            case ZipCompressionMethod.Explode:
            {
                context = context with { FormatOptions = Header.Flags };
                return await providers
                    .CreateDecompressStreamAsync(
                        compressionType,
                        stream,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            default:
            {
                return await providers
                    .CreateDecompressStreamAsync(
                        compressionType,
                        stream,
                        context,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask<Stream> CreateWinzipAesDecompressionStreamAsync(
        Stream stream,
        CancellationToken cancellationToken = default
    )
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

        return await CreateDecompressionStreamAsync(
                stream,
                (ZipCompressionMethod)
                    BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes.AsSpan(5)),
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
