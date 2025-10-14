using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Rar.Headers;

public class RarHeaderFactory
{
    private bool _isRar5;

    private Rar5CryptoInfo? _cryptInfo;

    public RarHeaderFactory(StreamingMode mode, ReaderOptions options)
    {
        StreamingMode = mode;
        Options = options;
    }

    public ReaderOptions Options { get; }
    public StreamingMode StreamingMode { get; }
    public bool IsEncrypted { get; private set; }

    public IEnumerable<IRarHeader> ReadHeaders(Stream stream)
    {
        var markHeader = MarkHeader.Read(stream, Options.LeaveStreamOpen, Options.LookForHeader);
        _isRar5 = markHeader.IsRar5;
        yield return markHeader;

        RarHeader? header;
        while ((header = TryReadNextHeader(stream)) != null)
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

    private RarHeader? TryReadNextHeader(Stream stream)
    {
        RarCrcBinaryReader reader;
        if (!IsEncrypted)
        {
            reader = new RarCrcBinaryReader(stream);
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
                _cryptInfo.ReadInitV(new MarkingBinaryReader(stream));
                var _headerKey = new CryptKey5(Options.Password!, _cryptInfo);

                reader = new RarCryptoBinaryReader(stream, _headerKey, _cryptInfo.Salt);
            }
            else
            {
                var key = new CryptKey3(Options.Password);
                reader = new RarCryptoBinaryReader(stream, key);
            }
        }

        var header = RarHeader.TryReadBase(reader, _isRar5, Options.ArchiveEncoding);
        if (header is null)
        {
            return null;
        }
        switch (header.HeaderCode)
        {
            case HeaderCodeV.RAR5_ARCHIVE_HEADER:
            case HeaderCodeV.RAR4_ARCHIVE_HEADER:
            {
                var ah = new ArchiveHeader(header, reader);
                if (ah.IsEncrypted == true)
                {
                    //!!! rar5 we don't know yet
                    IsEncrypted = true;
                }
                return ah;
            }

            case HeaderCodeV.RAR4_PROTECT_HEADER:
            {
                var ph = new ProtectHeader(header, reader);
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
                            reader.BaseStream.Skip(ph.DataSize);
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
                var fh = new FileHeader(header, reader, HeaderType.Service);
                if (fh.FileName == "CMT")
                {
                    fh.PackedStream = new ReadOnlySubStream(reader.BaseStream, fh.CompressedSize);
                }
                else
                {
                    SkipData(fh, reader);
                }
                return fh;
            }

            case HeaderCodeV.RAR4_NEW_SUB_HEADER:
            {
                var fh = new FileHeader(header, reader, HeaderType.NewSub);
                SkipData(fh, reader);
                return fh;
            }

            case HeaderCodeV.RAR5_FILE_HEADER:
            case HeaderCodeV.RAR4_FILE_HEADER:
            {
                var fh = new FileHeader(header, reader, HeaderType.File);
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
                                        : new CryptKey3(Options.Password)
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
                return new EndArchiveHeader(header, reader);
            }
            case HeaderCodeV.RAR5_ARCHIVE_ENCRYPTION_HEADER:
            {
                var cryptoHeader = new ArchiveCryptHeader(header, reader);
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

    private void SkipData(FileHeader fh, RarCrcBinaryReader reader)
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
                    reader.BaseStream.Skip(fh.CompressedSize);
                }
                break;
            default:
            {
                throw new InvalidFormatException("Invalid StreamingMode");
            }
        }
    }
}
