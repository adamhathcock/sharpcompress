using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal partial class ZipHeaderFactory
{
    protected async ValueTask<ZipHeader?> ReadHeader(
        uint headerBytes,
        AsyncBinaryReader reader,
        bool zip64 = false
    )
    {
        switch (headerBytes)
        {
            case ENTRY_HEADER_BYTES:
            {
                var entryHeader = new LocalEntryHeader(_archiveEncoding);
                await entryHeader.Read(reader);
                await LoadHeaderAsync(entryHeader, reader.BaseStream).ConfigureAwait(false);

                _lastEntryHeader = entryHeader;
                return entryHeader;
            }
            case DIRECTORY_START_HEADER_BYTES:
            {
                var entry = new DirectoryEntryHeader(_archiveEncoding);
                await entry.Read(reader);
                return entry;
            }
            case POST_DATA_DESCRIPTOR:
            {
                if (
                    _lastEntryHeader != null
                    && FlagUtility.HasFlag(
                        _lastEntryHeader.NotNull().Flags,
                        HeaderFlags.UsePostDataDescriptor
                    )
                )
                {
                    _lastEntryHeader.Crc = await reader.ReadUInt32Async();
                    _lastEntryHeader.CompressedSize = zip64
                        ? (long)await reader.ReadUInt64Async()
                        : await reader.ReadUInt32Async();
                    _lastEntryHeader.UncompressedSize = zip64
                        ? (long)await reader.ReadUInt64Async()
                        : await reader.ReadUInt32Async();
                }
                else
                {
                    await reader.SkipAsync(zip64 ? 20 : 12);
                }
                return null;
            }
            case DIGITAL_SIGNATURE:
                return null;
            case DIRECTORY_END_HEADER_BYTES:
            {
                var entry = new DirectoryEndHeader();
                await entry.Read(reader);
                return entry;
            }
            case SPLIT_ARCHIVE_HEADER_BYTES:
            {
                return new SplitHeader();
            }
            case ZIP64_END_OF_CENTRAL_DIRECTORY:
            {
                var entry = new Zip64DirectoryEndHeader();
                await entry.Read(reader);
                return entry;
            }
            case ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR:
            {
                var entry = new Zip64DirectoryEndLocatorHeader();
                await entry.Read(reader);
                return entry;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Loads encryption metadata and stream positioning for a header using async reads where needed.
    /// </summary>
    private async ValueTask LoadHeaderAsync(ZipFileEntry entryHeader, Stream stream)
    {
        if (FlagUtility.HasFlag(entryHeader.Flags, HeaderFlags.Encrypted))
        {
            if (
                !entryHeader.IsDirectory
                && entryHeader.CompressedSize == 0
                && FlagUtility.HasFlag(entryHeader.Flags, HeaderFlags.UsePostDataDescriptor)
            )
            {
                throw new NotSupportedException(
                    "SharpCompress cannot currently read non-seekable Zip Streams with encrypted data that has been written in a non-seekable manner."
                );
            }

            if (_password is null)
            {
                throw new CryptographicException("No password supplied for encrypted zip.");
            }

            entryHeader.Password = _password;

            if (entryHeader.CompressionMethod == ZipCompressionMethod.WinzipAes)
            {
                var data = entryHeader.Extra.SingleOrDefault(x =>
                    x.Type == ExtraDataType.WinZipAes
                );
                if (data != null)
                {
                    var keySize = (WinzipAesKeySize)data.DataBytes[4];

                    var salt = new byte[WinzipAesEncryptionData.KeyLengthInBytes(keySize) / 2];
                    var passwordVerifyValue = new byte[2];
                    await stream.ReadExactAsync(salt, 0, salt.Length).ConfigureAwait(false);
                    await stream.ReadExactAsync(passwordVerifyValue, 0, 2).ConfigureAwait(false);

                    entryHeader.WinzipAesEncryptionData = new WinzipAesEncryptionData(
                        keySize,
                        salt,
                        passwordVerifyValue,
                        _password
                    );

                    entryHeader.CompressedSize -= (uint)(salt.Length + 2);
                }
            }
        }

        if (entryHeader.IsDirectory)
        {
            return;
        }

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
    }
}
