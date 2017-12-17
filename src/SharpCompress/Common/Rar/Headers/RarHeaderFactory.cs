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
            while ((header = ReadNextHeader(stream)) != null)
            {
                yield return header;
                if (header.HeaderType == HeaderType.EndArchiveHeader)
                {
                    yield break; // the end?
                }
            }
        }

        private RarHeader ReadNextHeader(Stream stream)
        {
            RarCrcBinaryReader reader;
            if (!IsEncrypted) {
                reader = new RarCrcBinaryReader(stream);
            } else {
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

            var header = RarHeader.ReadBase(reader, Options.ArchiveEncoding, this.isRar5);
            if (header == null)
            {
                return null;
            }
            switch (header.HeaderType)
            {
                case HeaderType.Rar5ArchiveHeader:
                case HeaderType.ArchiveHeader:
                    {
                        var ah = new ArchiveHeader(header, reader);
                        //x var ah = header.PromoteHeader<ArchiveHeader>(reader);
                        IsEncrypted = ah.HasPassword;
                        return ah;
                    }

                case HeaderType.ProtectHeader:
                    {
                        ProtectHeader ph = header.PromoteHeader<ProtectHeader>(reader);

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

                case HeaderType.NewSubHeader:
                    {
                        FileHeader fh = header.PromoteHeader<FileHeader>(reader);
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
                        return fh;
                    }
                case HeaderType.FileHeader:
                    {
                        FileHeader fh = header.PromoteHeader<FileHeader>(reader);
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
                                    if (fh.Salt == null)
                                    {
                                        fh.PackedStream = ms;
                                    }
                                    else
                                    {
#if !NO_CRYPTO
                                        fh.PackedStream = new RarCryptoWrapper(ms, Options.Password, fh.Salt);
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
                case HeaderType.EndArchiveHeader:
                    {
                        return header.PromoteHeader<EndArchiveHeader>(reader);
                    }
                default:
                    {
                        throw new InvalidFormatException("Invalid Rar Header: " + header.HeaderType);
                    }
            }
        }
    }
}