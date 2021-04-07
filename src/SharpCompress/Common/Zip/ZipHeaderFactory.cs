using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
    internal class ZipHeaderFactory
    {
        internal const uint ENTRY_HEADER_BYTES = 0x04034b50;
        internal const uint POST_DATA_DESCRIPTOR = 0x08074b50;
        internal const uint DIRECTORY_START_HEADER_BYTES = 0x02014b50;
        internal const uint DIRECTORY_END_HEADER_BYTES = 0x06054b50;
        internal const uint DIGITAL_SIGNATURE = 0x05054b50;
        internal const uint SPLIT_ARCHIVE_HEADER_BYTES = 0x30304b50;

        internal const uint ZIP64_END_OF_CENTRAL_DIRECTORY = 0x06064b50;
        internal const uint ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR = 0x07064b50;

        protected LocalEntryHeader? _lastEntryHeader;
        private readonly string? _password;
        private readonly StreamingMode _mode;
        private readonly ArchiveEncoding _archiveEncoding;

        protected ZipHeaderFactory(StreamingMode mode, string? password, ArchiveEncoding archiveEncoding)
        {
            this._mode = mode;
            this._password = password;
            this._archiveEncoding = archiveEncoding;
        }

        protected async ValueTask<ZipHeader?> ReadHeader(uint headerBytes, Stream stream, CancellationToken cancellationToken, bool zip64 = false)
        {
            switch (headerBytes)
            {
                case ENTRY_HEADER_BYTES:
                    {
                        var entryHeader = new LocalEntryHeader(_archiveEncoding);
                        await entryHeader.Read(stream, cancellationToken);
                        await LoadHeader(entryHeader, stream, cancellationToken);

                        _lastEntryHeader = entryHeader;
                        return entryHeader;
                    }
                case DIRECTORY_START_HEADER_BYTES:
                    {
                        var entry = new DirectoryEntryHeader(_archiveEncoding);
                        await entry.Read(stream, cancellationToken);
                        return entry;
                    }
                case POST_DATA_DESCRIPTOR:
                    {
                        if (FlagUtility.HasFlag(_lastEntryHeader!.Flags, HeaderFlags.UsePostDataDescriptor))
                        {
                            _lastEntryHeader.Crc = await stream.ReadUInt32(cancellationToken);
                            _lastEntryHeader.CompressedSize = zip64 ? (long)await stream.ReadUInt64(cancellationToken) : await stream.ReadUInt32(cancellationToken);
                            _lastEntryHeader.UncompressedSize = zip64 ? (long)await stream.ReadUInt64(cancellationToken) : await stream.ReadUInt32(cancellationToken);
                        }
                        else
                        {
                            await stream.ReadBytes(zip64 ? 20 : 12, cancellationToken);
                        }
                        return null;
                    }
                case DIGITAL_SIGNATURE:
                    return null;
                case DIRECTORY_END_HEADER_BYTES:
                    {
                        var entry = new DirectoryEndHeader();
                        await entry.Read(stream, cancellationToken);
                        return entry;
                    }
                case SPLIT_ARCHIVE_HEADER_BYTES:
                    {
                        return new SplitHeader();
                    }
                case ZIP64_END_OF_CENTRAL_DIRECTORY:
                    {
                        var entry = new Zip64DirectoryEndHeader();
                        await entry.Read(stream, cancellationToken);
                        return entry;
                    }
                case ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR:
                    {
                        var entry = new Zip64DirectoryEndLocatorHeader();
                        await entry.Read(stream, cancellationToken);
                        return entry;
                    }
                default:
                    return null;
            }
        }

        internal static bool IsHeader(uint headerBytes)
        {
            switch (headerBytes)
            {
                case ENTRY_HEADER_BYTES:
                case DIRECTORY_START_HEADER_BYTES:
                case POST_DATA_DESCRIPTOR:
                case DIGITAL_SIGNATURE:
                case DIRECTORY_END_HEADER_BYTES:
                case SPLIT_ARCHIVE_HEADER_BYTES:
                case ZIP64_END_OF_CENTRAL_DIRECTORY:
                case ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR:
                    return true;
                default:
                    return false;
            }
        }

        private async ValueTask LoadHeader(ZipFileEntry entryHeader, Stream stream, CancellationToken cancellationToken)
        {
            if (FlagUtility.HasFlag(entryHeader.Flags, HeaderFlags.Encrypted))
            {
                if (!entryHeader.IsDirectory && entryHeader.CompressedSize == 0 &&
                    FlagUtility.HasFlag(entryHeader.Flags, HeaderFlags.UsePostDataDescriptor))
                {
                    throw new NotSupportedException("SharpCompress cannot currently read non-seekable Zip Streams with encrypted data that has been written in a non-seekable manner.");
                }

                if (_password is null)
                {
                    throw new CryptographicException("No password supplied for encrypted zip.");
                }

                entryHeader.Password = _password;

                if (entryHeader.CompressionMethod == ZipCompressionMethod.WinzipAes)
                {
                    ExtraData? data = entryHeader.Extra.SingleOrDefault(x => x.Type == ExtraDataType.WinZipAes);
                    if (data != null)
                    {
                        var keySize = (WinzipAesKeySize)data.DataBytes[4];

                        var salt = await stream.ReadBytes(WinzipAesEncryptionData.KeyLengthInBytes(keySize) / 2, cancellationToken);
                        var passwordVerifyValue = await stream.ReadBytes(2, cancellationToken);
                        entryHeader.WinzipAesEncryptionData =
                            new WinzipAesEncryptionData(keySize, salt, passwordVerifyValue, _password);

                        entryHeader.CompressedSize -= (uint)(salt.Length + 2);
                    }
                }
            }

            if (entryHeader.IsDirectory)
            {
                return;
            }

            //if (FlagUtility.HasFlag(entryHeader.Flags, HeaderFlags.UsePostDataDescriptor))
            //{
            //    entryHeader.PackedStream = new ReadOnlySubStream(stream);
            //}
            //else
            //{
            switch (_mode)
            {
                case StreamingMode.Seekable:
                    {
                        entryHeader.DataStartPosition = stream.Position;
                        stream.Position += entryHeader.CompressedSize;
                        break;
                    }

                case StreamingMode.Streaming:
                    {
                        entryHeader.PackedStream = stream;
                        break;
                    }

                default:
                    {
                        throw new InvalidFormatException("Invalid StreamingMode");
                    }
            }

            //}
        }
    }
}