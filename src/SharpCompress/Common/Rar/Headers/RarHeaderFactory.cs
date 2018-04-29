using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;
using SharpCompress.Readers;

namespace SharpCompress.Common.Rar.Headers
{
    internal class RarHeaderFactory
    {
        private bool isRar5;

        internal RarHeaderFactory(StreamingMode mode, ReaderOptions options)
        {
            StreamingMode = mode;
            Options = options;
        }

        private ReaderOptions Options { get; }
        internal StreamingMode StreamingMode { get; }
        internal bool IsEncrypted { get; private set; }

        internal IEnumerable<IRarHeader> ReadHeaders(Stream stream)
        {
            var markHeader = MarkHeader.Read(stream, Options.LeaveStreamOpen, Options.LookForHeader);
            this.isRar5 = markHeader.IsRar5;
            yield return markHeader;

            RarHeader header;
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

        private RarHeader TryReadNextHeader(Stream stream)
        {
            RarCrcBinaryReader reader;
            if (!IsEncrypted) 
            {
                reader = new RarCrcBinaryReader(stream);
            } 
            else 
            {
#if !NO_CRYPTO
                if (Options.Password == null)
                {
                    throw new CryptographicException("Encrypted Rar archive has no password specified.");
                }
                reader = new RarCryptoBinaryReader(stream, Options.Password);
#else
                throw new CryptographicException("Rar encryption unsupported on this platform");
#endif
            }

            var header = RarHeader.TryReadBase(reader, this.isRar5, Options.ArchiveEncoding);
            if (header == null)
            {
                return null;
            }
            switch (header.HeaderCode)
            {
                case HeaderCodeV.Rar5ArchiveHeader:
                case HeaderCodeV.Rar4ArchiveHeader:
                    {
                        var ah = new ArchiveHeader(header, reader);
                        if (ah.IsEncrypted == true) 
                        {
                            //!!! rar5 we don't know yet
                            IsEncrypted = true;
                        }
                        return ah;
                    }

                case HeaderCodeV.Rar4ProtectHeader:
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

                case HeaderCodeV.Rar5ServiceHeader:
                    {
                        var fh = new FileHeader(header, reader, HeaderType.Service);
                        SkipData(fh, reader);
                        return fh;
                    }

                case HeaderCodeV.Rar4NewSubHeader:
                    {
                        var fh = new FileHeader(header, reader, HeaderType.NewSub);
                        SkipData(fh, reader);
                        return fh;
                    }

                case HeaderCodeV.Rar5FileHeader:
                case HeaderCodeV.Rar4FileHeader:
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
                                    if (fh.R4Salt == null)
                                    {
                                        fh.PackedStream = ms;
                                    }
                                    else
                                    {
#if !NO_CRYPTO
                                        fh.PackedStream = new RarCryptoWrapper(ms, Options.Password, fh.R4Salt);
#else
                                        throw new NotSupportedException("RarCrypto not supported");
#endif
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
                case HeaderCodeV.Rar5EndArchiveHeader:
                case HeaderCodeV.Rar4EndArchiveHeader:
                    {
                        return new EndArchiveHeader(header, reader);
                    }
                case HeaderCodeV.Rar5ArchiveEncryptionHeader:
                {
                    var ch = new ArchiveCryptHeader(header, reader);
                    IsEncrypted = true;
                    return ch;
                }
                default:
                    {
                        throw new InvalidFormatException("Unknown Rar Header: " + header.HeaderCode);
                    }
            }
        }

        private void SkipData(FileHeader fh, RarCrcBinaryReader reader) {
            switch (StreamingMode) {
                case StreamingMode.Seekable: {
                    fh.DataStartPosition = reader.BaseStream.Position;
                    reader.BaseStream.Position += fh.CompressedSize;
                }
                    break;
                case StreamingMode.Streaming: {
                    //skip the data because it's useless?
                    reader.BaseStream.Skip(fh.CompressedSize);
                }
                    break;
                default: {
                    throw new InvalidFormatException("Invalid StreamingMode");
                }
            }
        }
    }
}