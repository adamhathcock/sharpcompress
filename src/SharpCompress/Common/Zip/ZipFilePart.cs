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
using SharpCompress.IO;
using ZstdSharp;

namespace SharpCompress.Common.Zip;

internal abstract class ZipFilePart : FilePart
{
    internal ZipFilePart(ZipFileEntry header, Stream stream)
        : base(header.ArchiveEncoding)
    {
        Header = header;
        header.Part = this;
        BaseStream = stream;
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
            return NonDisposingStream.Create(decompressionStream);
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

    protected Stream CreateDecompressionStream(Stream stream, ZipCompressionMethod method)
    {
        switch (method)
        {
            case ZipCompressionMethod.None:
            {
                if (stream is ReadOnlySubStream)
                {
                    return stream;
                }

                if (Header.CompressedSize > 0)
                {
                    return new ReadOnlySubStream(stream, Header.CompressedSize);
                }

                return new DataDescriptorStream(stream);
            }
            case ZipCompressionMethod.Shrink:
            {
                return new ShrinkStream(
                    stream,
                    CompressionMode.Decompress,
                    Header.CompressedSize,
                    Header.UncompressedSize
                );
            }
            case ZipCompressionMethod.Reduce1:
            {
                return new ReduceStream(stream, Header.CompressedSize, Header.UncompressedSize, 1);
            }
            case ZipCompressionMethod.Reduce2:
            {
                return new ReduceStream(stream, Header.CompressedSize, Header.UncompressedSize, 2);
            }
            case ZipCompressionMethod.Reduce3:
            {
                return new ReduceStream(stream, Header.CompressedSize, Header.UncompressedSize, 3);
            }
            case ZipCompressionMethod.Reduce4:
            {
                return new ReduceStream(stream, Header.CompressedSize, Header.UncompressedSize, 4);
            }
            case ZipCompressionMethod.Explode:
            {
                return new ExplodeStream(
                    stream,
                    Header.CompressedSize,
                    Header.UncompressedSize,
                    Header.Flags
                );
            }

            case ZipCompressionMethod.Deflate:
            {
                return new DeflateStream(stream, CompressionMode.Decompress);
            }
            case ZipCompressionMethod.Deflate64:
            {
                return new Deflate64Stream(stream, CompressionMode.Decompress);
            }
            case ZipCompressionMethod.BZip2:
            {
                return new BZip2Stream(stream, CompressionMode.Decompress, false);
            }
            case ZipCompressionMethod.LZMA:
            {
                if (FlagUtility.HasFlag(Header.Flags, HeaderFlags.Encrypted))
                {
                    throw new NotSupportedException("LZMA with pkware encryption.");
                }
                var reader = new BinaryReader(stream);
                reader.ReadUInt16(); //LZMA version
                var props = new byte[reader.ReadUInt16()];
                reader.Read(props, 0, props.Length);
                return new LzmaStream(
                    props,
                    stream,
                    Header.CompressedSize > 0 ? Header.CompressedSize - 4 - props.Length : -1,
                    FlagUtility.HasFlag(Header.Flags, HeaderFlags.Bit1)
                        ? -1
                        : Header.UncompressedSize
                );
            }
            case ZipCompressionMethod.Xz:
            {
                return new XZStream(stream);
            }
            case ZipCompressionMethod.ZStd:
            {
                return new DecompressionStream(stream);
            }
            case ZipCompressionMethod.PPMd:
            {
                Span<byte> props = stackalloc byte[2];
                stream.ReadFully(props);
                return new PpmdStream(new PpmdProperties(props), stream, false);
            }
            case ZipCompressionMethod.WinzipAes:
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
                    throw new InvalidFormatException(
                        "Unexpected vendor ID for WinZip AES metadata"
                    );
                }
                return CreateDecompressionStream(
                    stream,
                    (ZipCompressionMethod)
                        BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes.AsSpan(5))
                );
            }
            default:
            {
                throw new NotSupportedException("CompressionMethod: " + Header.CompressionMethod);
            }
        }
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
            plainStream = NonDisposingStream.Create(plainStream); //make sure AES doesn't close
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
                    throw new InvalidOperationException("Header.CompressionMethod is invalid");
                }
            }
        }
        return plainStream;
    }
}
