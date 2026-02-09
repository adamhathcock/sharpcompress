using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Rar;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Rar.Headers;

public partial class RarHeaderFactory
{
    public async IAsyncEnumerable<IRarHeader> ReadHeadersAsync(Stream stream)
    {
        var markHeader = await MarkHeader
            .ReadAsync(
                stream,
                Options.LeaveStreamOpen,
                Options.LookForHeader,
                CancellationToken.None
            )
            .ConfigureAwait(false);
        _isRar5 = markHeader.IsRar5;
        yield return markHeader;

        RarHeader? header;
        while (
            (
                header = await TryReadNextHeaderAsync(stream, CancellationToken.None)
                    .ConfigureAwait(false)
            ) != null
        )
        {
            yield return header;
            if (header.HeaderType == HeaderType.EndArchive)
            {
                // End of archive marker. RAR does not read anything after this header letting to use third
                // party tools to add extra information such as a digital signature to archive.
                yield break;
            }
        }
    }

    private async ValueTask<RarHeader?> TryReadNextHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken
    )
    {
        AsyncRarCrcBinaryReader reader;
        if (!IsEncrypted)
        {
            reader = new AsyncRarCrcBinaryReader(stream);
        }
        else
        {
            if (Options.Password is null)
            {
                throw new CryptographicException(
                    "Encrypted Rar archive has no password specified."
                );
            }

            if (_isRar5 && _cryptInfo != null)
            {
                await _cryptInfo.ReadInitVAsync(new AsyncMarkingBinaryReader(stream));
                var _headerKey = new CryptKey5(Options.Password!, _cryptInfo);

                reader = await AsyncRarCryptoBinaryReader.Create(
                    stream,
                    _headerKey,
                    _cryptInfo.Salt
                );
            }
            else
            {
                var key = new CryptKey3(Options.Password);
                reader = await AsyncRarCryptoBinaryReader.Create(stream, key);
            }
        }

        var header = await RarHeader
            .TryReadBaseAsync(reader, _isRar5, Options.ArchiveEncoding, cancellationToken)
            .ConfigureAwait(false);
        if (header is null)
        {
            return null;
        }
        switch (header.HeaderCode)
        {
            case HeaderCodeV.RAR5_ARCHIVE_HEADER:
            case HeaderCodeV.RAR4_ARCHIVE_HEADER:
            {
                var ah = await ArchiveHeader
                    .CreateAsync(header, reader, cancellationToken)
                    .ConfigureAwait(false);
                if (ah.IsEncrypted == true)
                {
                    //!!! rar5 we don't know yet
                    IsEncrypted = true;
                }
                return ah;
            }

            case HeaderCodeV.RAR4_PROTECT_HEADER:
            {
                var ph = await ProtectHeader
                    .CreateAsync(header, reader, cancellationToken)
                    .ConfigureAwait(false);
                // skip the recovery record data, we do not use it.
                switch (StreamingMode)
                {
                    case StreamingMode.Seekable:
                        {
                            reader.BaseStream.Position += ph.DataSize;
                        }
                        break;
                    case StreamingMode.Streaming:
                        {
                            await reader
                                .BaseStream.SkipAsync(ph.DataSize, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        break;
                    default:
                    {
                        throw new InvalidFormatException("Invalid StreamingMode");
                    }
                }
                return ph;
            }

            case HeaderCodeV.RAR5_SERVICE_HEADER:
            {
                var fh = await FileHeader
                    .CreateAsync(header, reader, HeaderType.Service, cancellationToken)
                    .ConfigureAwait(false);
                if (fh.FileName == "CMT")
                {
                    fh.PackedStream = new ReadOnlySubStream(reader.BaseStream, fh.CompressedSize);
                }
                else
                {
                    await SkipDataAsync(fh, reader, cancellationToken).ConfigureAwait(false);
                }
                return fh;
            }

            case HeaderCodeV.RAR4_NEW_SUB_HEADER:
            {
                var fh = await FileHeader
                    .CreateAsync(header, reader, HeaderType.NewSub, cancellationToken)
                    .ConfigureAwait(false);
                await SkipDataAsync(fh, reader, cancellationToken).ConfigureAwait(false);
                return fh;
            }

            case HeaderCodeV.RAR5_FILE_HEADER:
            case HeaderCodeV.RAR4_FILE_HEADER:
            {
                var fh = await FileHeader
                    .CreateAsync(header, reader, HeaderType.File, cancellationToken)
                    .ConfigureAwait(false);
                switch (StreamingMode)
                {
                    case StreamingMode.Seekable:
                        {
                            fh.DataStartPosition = reader.BaseStream.Position;
                            reader.BaseStream.Position += fh.CompressedSize;
                        }
                        break;
                    case StreamingMode.Streaming:
                        {
                            var ms = new ReadOnlySubStream(reader.BaseStream, fh.CompressedSize);
                            if (fh.R4Salt is null && fh.Rar5CryptoInfo is null)
                            {
                                fh.PackedStream = ms;
                            }
                            else
                            {
                                fh.PackedStream = new RarCryptoWrapper(
                                    ms,
                                    fh.R4Salt is null
                                        ? fh.Rar5CryptoInfo.NotNull().Salt
                                        : fh.R4Salt,
                                    fh.R4Salt is null
                                        ? new CryptKey5(
                                            Options.Password,
                                            fh.Rar5CryptoInfo.NotNull()
                                        )
                                        : new CryptKey3(Options.Password.NotNull())
                                );
                            }
                        }
                        break;
                    default:
                    {
                        throw new InvalidFormatException("Invalid StreamingMode");
                    }
                }
                return fh;
            }
            case HeaderCodeV.RAR5_END_ARCHIVE_HEADER:
            case HeaderCodeV.RAR4_END_ARCHIVE_HEADER:
            {
                return await EndArchiveHeader
                    .CreateAsync(header, reader, cancellationToken)
                    .ConfigureAwait(false);
            }
            case HeaderCodeV.RAR5_ARCHIVE_ENCRYPTION_HEADER:
            {
                var cryptoHeader = await ArchiveCryptHeader
                    .CreateAsync(header, reader, cancellationToken)
                    .ConfigureAwait(false);
                IsEncrypted = true;
                _cryptInfo = cryptoHeader.CryptInfo;

                return cryptoHeader;
            }
            default:
            {
                throw new InvalidFormatException("Unknown Rar Header: " + header.HeaderCode);
            }
        }
    }

    private async ValueTask SkipDataAsync(
        FileHeader fh,
        AsyncRarCrcBinaryReader reader,
        CancellationToken cancellationToken
    )
    {
        switch (StreamingMode)
        {
            case StreamingMode.Seekable:
                {
                    fh.DataStartPosition = reader.BaseStream.Position;
                    reader.BaseStream.Position += fh.CompressedSize;
                }
                break;
            case StreamingMode.Streaming:
                {
                    //skip the data because it's useless?
                    await reader
                        .BaseStream.SkipAsync(fh.CompressedSize, cancellationToken)
                        .ConfigureAwait(false);
                }
                break;
            default:
            {
                throw new InvalidFormatException("Invalid StreamingMode");
            }
        }
    }
}
