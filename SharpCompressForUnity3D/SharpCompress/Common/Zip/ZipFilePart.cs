namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.Compressor;
    using SharpCompress.Compressor.BZip2;
    using SharpCompress.Compressor.Deflate;
    using SharpCompress.Compressor.LZMA;
    using SharpCompress.Compressor.PPMd;
    using SharpCompress.IO;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;

    internal abstract class ZipFilePart : FilePart
    {
        [CompilerGenerated]
        private Stream <BaseStream>k__BackingField;
        [CompilerGenerated]
        private ZipFileEntry <Header>k__BackingField;

        internal ZipFilePart(ZipFileEntry header, Stream stream)
        {
            this.Header = header;
            header.Part = this;
            this.BaseStream = stream;
        }

        protected abstract Stream CreateBaseStream();
        protected Stream CreateDecompressionStream(Stream stream)
        {
            byte[] buffer;
            switch (this.Header.CompressionMethod)
            {
                case ZipCompressionMethod.None:
                    return stream;

                case ZipCompressionMethod.Deflate:
                    return new DeflateStream(stream, CompressionMode.Decompress, CompressionLevel.Default, false);

                case ZipCompressionMethod.BZip2:
                    return new BZip2Stream(stream, CompressionMode.Decompress, false, false);

                case ZipCompressionMethod.LZMA:
                {
                    if (FlagUtility.HasFlag<HeaderFlags>(this.Header.Flags, HeaderFlags.Encrypted))
                    {
                        throw new NotSupportedException("LZMA with pkware encryption.");
                    }
                    BinaryReader reader = new BinaryReader(stream);
                    reader.ReadUInt16();
                    buffer = new byte[reader.ReadUInt16()];
                    reader.Read(buffer, 0, buffer.Length);
                    return new LzmaStream(buffer, stream, (this.Header.CompressedSize > 0) ? ((this.Header.CompressedSize - 4) - buffer.Length) : -1L, FlagUtility.HasFlag<HeaderFlags>(this.Header.Flags, HeaderFlags.Bit1) ? -1L : ((long) this.Header.UncompressedSize));
                }
                case ZipCompressionMethod.PPMd:
                    buffer = new byte[2];
                    stream.Read(buffer, 0, buffer.Length);
                    return new PpmdStream(new PpmdProperties(buffer), stream, false);

                case ZipCompressionMethod.WinzipAes:
                {
                    ExtraData data = Enumerable.SingleOrDefault<ExtraData>(Enumerable.Where<ExtraData>(this.Header.Extra, delegate (ExtraData x) {
                        return x.Type == ExtraDataType.WinZipAes;
                    }));
                    if (data == null)
                    {
                        throw new InvalidFormatException("No Winzip AES extra data found.");
                    }
                    if (data.Length != 7)
                    {
                        throw new InvalidFormatException("Winzip data length is not 7.");
                    }
                    ushort num = BitConverter.ToUInt16(data.DataBytes, 0);
                    if ((num != 1) && (num != 2))
                    {
                        throw new InvalidFormatException("Unexpected vendor version number for WinZip AES metadata");
                    }
                    if (BitConverter.ToUInt16(data.DataBytes, 2) != 0x4541)
                    {
                        throw new InvalidFormatException("Unexpected vendor ID for WinZip AES metadata");
                    }
                    this.Header.CompressionMethod = (ZipCompressionMethod) BitConverter.ToUInt16(data.DataBytes, 5);
                    return this.CreateDecompressionStream(stream);
                }
            }
            throw new NotSupportedException("CompressionMethod: " + this.Header.CompressionMethod);
        }

        internal override Stream GetCompressedStream()
        {
            if (!this.Header.HasData)
            {
                return Stream.Null;
            }
            Stream stream = this.CreateDecompressionStream(this.GetCryptoStream(this.CreateBaseStream()));
            if (this.LeaveStreamOpen)
            {
                return new NonDisposingStream(stream);
            }
            return stream;
        }

        protected Stream GetCryptoStream(Stream plainStream)
        {
            if ((this.Header.CompressedSize == 0) && (this.Header.PkwareTraditionalEncryptionData != null))
            {
                throw new NotSupportedException("Cannot encrypt file with unknown size at start.");
            }
            if ((this.Header.CompressedSize == 0) && FlagUtility.HasFlag<HeaderFlags>(this.Header.Flags, HeaderFlags.UsePostDataDescriptor))
            {
                plainStream = new NonDisposingStream(plainStream);
            }
            else
            {
                plainStream = new ReadOnlySubStream(plainStream, (long) this.Header.CompressedSize);
            }
            if (this.Header.PkwareTraditionalEncryptionData != null)
            {
                return new PkwareTraditionalCryptoStream(plainStream, this.Header.PkwareTraditionalEncryptionData, CryptoMode.Decrypt);
            }
            return plainStream;
        }

        internal override Stream GetRawStream()
        {
            if (!this.Header.HasData)
            {
                return Stream.Null;
            }
            return this.CreateBaseStream();
        }

        internal Stream BaseStream
        {
            [CompilerGenerated]
            get
            {
                return this.<BaseStream>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<BaseStream>k__BackingField = value;
            }
        }

        internal override string FilePartName
        {
            get
            {
                return this.Header.Name;
            }
        }

        internal ZipFileEntry Header
        {
            [CompilerGenerated]
            get
            {
                return this.<Header>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Header>k__BackingField = value;
            }
        }

        protected bool LeaveStreamOpen
        {
            get
            {
                return FlagUtility.HasFlag<HeaderFlags>(this.Header.Flags, HeaderFlags.UsePostDataDescriptor);
            }
        }
    }
}

