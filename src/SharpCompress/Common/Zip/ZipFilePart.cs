using System;
using System.IO;
using System.Linq;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressor;
using SharpCompress.Compressor.BZip2;
using SharpCompress.Compressor.Deflate;
using SharpCompress.Compressor.LZMA;
using SharpCompress.Compressor.PPMd;
using SharpCompress.Converter;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
    internal abstract class ZipFilePart : FilePart
    {
        internal ZipFilePart(ZipFileEntry header, Stream stream)
        {
            Header = header;
            header.Part = this;
            this.BaseStream = stream;
        }

        internal Stream BaseStream { get; private set; }
        internal ZipFileEntry Header { get; set; }


        internal override string FilePartName
        {
            get { return Header.Name; }
        }

        internal override Stream GetCompressedStream()
        {
            if (!Header.HasData)
            {
                return Stream.Null;
            }
            Stream decompressionStream = CreateDecompressionStream(GetCryptoStream(CreateBaseStream()));
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

        protected bool LeaveStreamOpen
        {
            get { return FlagUtility.HasFlag(Header.Flags, HeaderFlags.UsePostDataDescriptor); }
        }

        protected Stream CreateDecompressionStream(Stream stream)
        {
            switch (Header.CompressionMethod)
            {
                case ZipCompressionMethod.None:
                    {
                        return stream;
                    }
                case ZipCompressionMethod.Deflate:
                    {
                        return new DeflateStream(stream, CompressionMode.Decompress);
                    }
                case ZipCompressionMethod.BZip2:
                    {
                        return new BZip2Stream(stream, CompressionMode.Decompress);
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
                        return new LzmaStream(props, stream,
                                              Header.CompressedSize > 0 ? Header.CompressedSize - 4 - props.Length : -1,
                                              FlagUtility.HasFlag(Header.Flags, HeaderFlags.Bit1)
                                                  ? -1
                                                  : (long)Header.UncompressedSize);
                    }
                case ZipCompressionMethod.PPMd:
                    {
                        var props = new byte[2];
                        stream.Read(props, 0, props.Length);
                        return new PpmdStream(new PpmdProperties(props), stream, false);
                    }
                case ZipCompressionMethod.WinzipAes:
                    {
                        ExtraData data = Header.Extra.Where(x => x.Type == ExtraDataType.WinZipAes).SingleOrDefault();
                        if (data == null)
                        {
                            throw new InvalidFormatException("No Winzip AES extra data found.");
                        }
                        if (data.Length != 7)
                        {
                            throw new InvalidFormatException("Winzip data length is not 7.");
                        }
                        ushort method = DataConverter.LittleEndian.GetUInt16(data.DataBytes, 0);

                        if (method != 0x01 && method != 0x02)
                        {
                            throw new InvalidFormatException("Unexpected vendor version number for WinZip AES metadata");
                        }

                        ushort vendorId = DataConverter.LittleEndian.GetUInt16(data.DataBytes, 2);
                        if (vendorId != 0x4541)
                        {
                            throw new InvalidFormatException("Unexpected vendor ID for WinZip AES metadata");
                        }
                        Header.CompressionMethod = (ZipCompressionMethod)DataConverter.LittleEndian.GetUInt16(data.DataBytes, 5);
                        return CreateDecompressionStream(stream);
                    }
                default:
                    {
                        throw new NotSupportedException("CompressionMethod: " + Header.CompressionMethod);
                    }
            }
        }

        protected Stream GetCryptoStream(Stream plainStream)
        {
            if ((Header.CompressedSize == 0)
#if !NO_CRYPTO
 && ((Header.PkwareTraditionalEncryptionData != null)
                    || (Header.WinzipAesEncryptionData != null)))
#else 
                && (Header.PkwareTraditionalEncryptionData != null))
#endif
            {
                throw new NotSupportedException("Cannot encrypt file with unknown size at start.");
            }
            if ((Header.CompressedSize == 0)
                && FlagUtility.HasFlag(Header.Flags, HeaderFlags.UsePostDataDescriptor))
            {
                plainStream = new NonDisposingStream(plainStream); //make sure AES doesn't close    
            }
            else
            {
                plainStream = new ReadOnlySubStream(plainStream, Header.CompressedSize); //make sure AES doesn't close
            }
            if (Header.PkwareTraditionalEncryptionData != null)
            {
                return new PkwareTraditionalCryptoStream(plainStream, Header.PkwareTraditionalEncryptionData,
                                                         CryptoMode.Decrypt);
            }
#if !NO_FILE
            if (Header.WinzipAesEncryptionData != null)
            {
                //only read 10 less because the last ten are auth bytes
                return new WinzipAesCryptoStream(plainStream, Header.WinzipAesEncryptionData, Header.CompressedSize - 10);
            }
#endif
            return plainStream;
        }
    }
}