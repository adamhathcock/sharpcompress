using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.Deflate64;
using SharpCompress.Compressors.LZMA;
//using SharpCompress.Compressors.PPMd;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
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

        internal override string FilePartName => Header.Name;

        internal override async ValueTask<Stream> GetCompressedStreamAsync(CancellationToken cancellationToken)
        {
            if (!Header.HasData)
            {
                return Stream.Null;
            }
            Stream decompressionStream = await CreateDecompressionStream(GetCryptoStream(CreateBaseStream()), Header.CompressionMethod, cancellationToken);
            if (LeaveStreamOpen)
            {
                return new NonDisposingStream(decompressionStream);
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

        protected bool LeaveStreamOpen => FlagUtility.HasFlag(Header.Flags, HeaderFlags.UsePostDataDescriptor) || Header.IsZip64;

        protected async ValueTask<Stream> CreateDecompressionStream(Stream stream, ZipCompressionMethod method, CancellationToken cancellationToken)
        {
            switch (method)
            {
                case ZipCompressionMethod.None:
                    {
                        return stream;
                    }
                case ZipCompressionMethod.Deflate:
                    {
                        return new DeflateStream(stream, CompressionMode.Decompress);
                    }
                case ZipCompressionMethod.Deflate64:
                    {
                        return new Deflate64Stream(stream, CompressionMode.Decompress);
                    }
               /**  case ZipCompressionMethod.BZip2:
                    {
                        return await BZip2Stream.CreateAsync(stream, CompressionMode.Decompress, false, cancellationToken);
                    }                                                                                          */
                case ZipCompressionMethod.LZMA:
                    {
                        if (FlagUtility.HasFlag(Header.Flags, HeaderFlags.Encrypted))
                        {
                            throw new NotSupportedException("LZMA with pkware encryption.");
                        }
                        await stream.ReadUInt16(cancellationToken); //LZMA version
                        var props = new byte[await stream.ReadUInt16(cancellationToken)];
                        await stream.ReadAsync(props, 0, props.Length, cancellationToken);
                        return await LzmaStream.CreateAsync(props, stream,
                                              Header.CompressedSize > 0 ? Header.CompressedSize - 4 - props.Length : -1,
                                              FlagUtility.HasFlag(Header.Flags, HeaderFlags.Bit1)
                                                  ? -1
                                                  : (long)Header.UncompressedSize);
                    }
              /*  case ZipCompressionMethod.PPMd:
                {
                    var props = await stream.ReadBytes(2, cancellationToken);
                        return new PpmdStream(new PpmdProperties(props), stream, false);
                    }  */
                case ZipCompressionMethod.WinzipAes:
                    {
                        ExtraData? data = Header.Extra.SingleOrDefault(x => x.Type == ExtraDataType.WinZipAes);
                        if (data is null)
                        {
                            throw new InvalidFormatException("No Winzip AES extra data found.");
                        }
                        if (data.Length != 7)
                        {
                            throw new InvalidFormatException("Winzip data length is not 7.");
                        }
                        ushort compressedMethod = BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes);

                        if (compressedMethod != 0x01 && compressedMethod != 0x02)
                        {
                            throw new InvalidFormatException("Unexpected vendor version number for WinZip AES metadata");
                        }

                        ushort vendorId = BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes.AsSpan(2));
                        if (vendorId != 0x4541)
                        {
                            throw new InvalidFormatException("Unexpected vendor ID for WinZip AES metadata");
                        }
                        return await CreateDecompressionStream(stream, (ZipCompressionMethod)BinaryPrimitives.ReadUInt16LittleEndian(data.DataBytes.AsSpan(5)), cancellationToken);
                    }
                default:
                    {
                        throw new NotSupportedException("CompressionMethod: " + Header.CompressionMethod);
                    }
            }
        }

        protected Stream GetCryptoStream(Stream plainStream)
        {
            bool isFileEncrypted = FlagUtility.HasFlag(Header.Flags, HeaderFlags.Encrypted);

            if (Header.CompressedSize == 0 && isFileEncrypted)
            {
                throw new NotSupportedException("Cannot encrypt file with unknown size at start.");
            }

            if ((Header.CompressedSize == 0
                && FlagUtility.HasFlag(Header.Flags, HeaderFlags.UsePostDataDescriptor))
                || Header.IsZip64)
            {
                plainStream = new NonDisposingStream(plainStream); //make sure AES doesn't close
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
                    case ZipCompressionMethod.Deflate:
                    case ZipCompressionMethod.Deflate64:
                    case ZipCompressionMethod.BZip2:
                    case ZipCompressionMethod.LZMA:
                    case ZipCompressionMethod.PPMd:
                        {
                            return new PkwareTraditionalCryptoStream(plainStream, Header.ComposeEncryptionData(plainStream), CryptoMode.Decrypt);
                        }

                    case ZipCompressionMethod.WinzipAes:
                        {
                            if (Header.WinzipAesEncryptionData != null)
                            {
                                return new WinzipAesCryptoStream(plainStream, Header.WinzipAesEncryptionData, Header.CompressedSize - 10);
                            }
                            return plainStream;
                        }

                    default:
                        {
                            throw new ArgumentOutOfRangeException();
                        }

                }
            }
            return plainStream;
        }
    }
}
