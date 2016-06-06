using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.IO;

namespace SharpCompress.Common.Rar.Headers
{
    internal class RarHeaderFactory
    {
        private const int MAX_SFX_SIZE = 0x80000 - 16; //archive.cpp line 136

        internal RarHeaderFactory(StreamingMode mode, Options options, string password = null)
        {
            StreamingMode = mode;
            Options = options;
            Password = password;
        }

        private Options Options { get; set; }
        public string Password { get; private set; }
        internal StreamingMode StreamingMode { get; private set; }
        internal bool IsEncrypted { get; private set; }
        
        internal IEnumerable<RarHeader> ReadHeaders(Stream stream)
        {
            if (Options.HasFlag(Options.LookForHeader))
            {
                stream = CheckSFX(stream);
            }

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

        private Stream CheckSFX(Stream stream)
        {
            RewindableStream rewindableStream = GetRewindableStream(stream);
            stream = rewindableStream;
            BinaryReader reader = new BinaryReader(rewindableStream);
            try
            {
                int count = 0;
                while (true)
                {
                    byte firstByte = reader.ReadByte();
                    if (firstByte == 0x52)
                    {
                        MemoryStream buffer = new MemoryStream();
                        byte[] nextThreeBytes = reader.ReadBytes(3);
                        if ((nextThreeBytes[0] == 0x45)
                            && (nextThreeBytes[1] == 0x7E)
                            && (nextThreeBytes[2] == 0x5E))
                        {
                            //old format and isvalid
                            buffer.WriteByte(0x52);
                            buffer.Write(nextThreeBytes, 0, 3);
                            rewindableStream.Rewind(buffer);
                            break;
                        }
                        byte[] secondThreeBytes = reader.ReadBytes(3);
                        if ((nextThreeBytes[0] == 0x61)
                            && (nextThreeBytes[1] == 0x72)
                            && (nextThreeBytes[2] == 0x21)
                            && (secondThreeBytes[0] == 0x1A)
                            && (secondThreeBytes[1] == 0x07)
                            && (secondThreeBytes[2] == 0x00))
                        {
                            //new format and isvalid
                            buffer.WriteByte(0x52);
                            buffer.Write(nextThreeBytes, 0, 3);
                            buffer.Write(secondThreeBytes, 0, 3);
                            rewindableStream.Rewind(buffer);
                            break;
                        }
                        buffer.Write(nextThreeBytes, 0, 3);
                        buffer.Write(secondThreeBytes, 0, 3);
                        rewindableStream.Rewind(buffer);
                    }
                    if (count > MAX_SFX_SIZE)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                if (!Options.HasFlag(Options.KeepStreamsOpen))
                {
#if NET35
                    reader.Close();
#else
                    reader.Dispose();
#endif
                }
                throw new InvalidFormatException("Error trying to read rar signature.", e);
            }
            return stream;
        }

        private RewindableStream GetRewindableStream(Stream stream)
        {
            RewindableStream rewindableStream = stream as RewindableStream;
            if (rewindableStream == null)
            {
                rewindableStream = new RewindableStream(stream);
            }
            return rewindableStream;
        }


        private RarHeader ReadNextHeader(Stream stream)
        {
#if !NO_CRYPTO
            var reader = new RarCryptoBinaryReader(stream, Password);
            
            if (IsEncrypted)
            {
                if (Password == null)
                {
                    throw new CryptographicException("Encrypted Rar archive has no password specified.");
                }
                reader.SkipQueue();
                byte[] salt = reader.ReadBytes(8);
                reader.InitializeAes(salt);
            }
#else
            var reader = new MarkingBinaryReader(stream);

#endif

            RarHeader header = RarHeader.Create(reader);
            if (header == null)
            {
                return null;
            }
            switch (header.HeaderType)
            {
                case HeaderType.ArchiveHeader:
                    {
                        var ah = header.PromoteHeader<ArchiveHeader>(reader);
                        IsEncrypted = ah.HasPassword;
                        return ah;
                    }
                case HeaderType.MarkHeader:
                    {
                        return header.PromoteHeader<MarkHeader>(reader);
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
                                        fh.PackedStream = new RarCryptoWrapper(ms, Password,  fh.Salt);
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
                        throw new InvalidFormatException("Invalid Rar Header: " + header.HeaderType.ToString());
                    }
            }
        }
    }
}