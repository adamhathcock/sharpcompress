namespace SharpCompress.Common.Rar.Headers
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using SharpCompress.IO;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;

    internal class RarHeaderFactory
    {
        [CompilerGenerated]
        private bool <IsEncrypted>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.Common.Options <Options>k__BackingField;
        [CompilerGenerated]
        private string <Password>k__BackingField;
        [CompilerGenerated]
        private SharpCompress.IO.StreamingMode <StreamingMode>k__BackingField;
        private const int MAX_SFX_SIZE = 0x7fff0;

        internal RarHeaderFactory(SharpCompress.IO.StreamingMode mode, SharpCompress.Common.Options options, [Optional, DefaultParameterValue(null)] string password)
        {
            this.StreamingMode = mode;
            this.Options = options;
            this.Password = password;
        }

        private Stream CheckSFX(Stream stream)
        {
            RewindableStream rewindableStream = this.GetRewindableStream(stream);
            stream = rewindableStream;
            BinaryReader reader = new BinaryReader(rewindableStream);
            try
            {
                bool flag;
                int num = 0;
                goto Label_0138;
            Label_001B:
                if (reader.ReadByte() == 0x52)
                {
                    MemoryStream stream3 = new MemoryStream();
                    byte[] buffer = reader.ReadBytes(3);
                    if (((buffer[0] == 0x45) && (buffer[1] == 0x7e)) && (buffer[2] == 0x5e))
                    {
                        stream3.WriteByte(0x52);
                        stream3.Write(buffer, 0, 3);
                        rewindableStream.Rewind(stream3);
                        return stream;
                    }
                    byte[] buffer2 = reader.ReadBytes(3);
                    if (((((buffer[0] == 0x61) && (buffer[1] == 0x72)) && ((buffer[2] == 0x21) && (buffer2[0] == 0x1a))) && (buffer2[1] == 7)) && (buffer2[2] == 0))
                    {
                        stream3.WriteByte(0x52);
                        stream3.Write(buffer, 0, 3);
                        stream3.Write(buffer2, 0, 3);
                        rewindableStream.Rewind(stream3);
                        return stream;
                    }
                    stream3.Write(buffer, 0, 3);
                    stream3.Write(buffer2, 0, 3);
                    rewindableStream.Rewind(stream3);
                }
                if (num > 0x7fff0)
                {
                    return stream;
                }
            Label_0138:
                flag = true;
                goto Label_001B;
            }
            catch (Exception exception)
            {
                if (!this.Options_HasFlag(SharpCompress.Common.Options.KeepStreamsOpen))
                {
                    reader.Close();
                }
                throw new InvalidFormatException("Error trying to read rar signature.", exception);
            }
            return stream;
        }

        private RewindableStream GetRewindableStream(Stream stream)
        {
            RewindableStream stream2 = stream as RewindableStream;
            if (stream2 == null)
            {
                stream2 = new RewindableStream(stream);
            }
            return stream2;
        }

        private bool Options_HasFlag(SharpCompress.Common.Options options)
        {
            return ((this.Options & options) == options);
        }

        internal IEnumerable<RarHeader> ReadHeaders(Stream stream)
        {
            RarHeader iteratorVariable0;
            if (this.Options_HasFlag(SharpCompress.Common.Options.LookForHeader))
            {
                stream = this.CheckSFX(stream);
            }
            while ((iteratorVariable0 = this.ReadNextHeader(stream)) != null)
            {
                yield return iteratorVariable0;
                if (iteratorVariable0.HeaderType == HeaderType.EndArchiveHeader)
                {
                    break;
                }
            }
        }

        private RarHeader ReadNextHeader(Stream stream)
        {
            FileHeader header4;
            RarCryptoBinaryReader reader = new RarCryptoBinaryReader(stream, this.Password);
            if (this.IsEncrypted)
            {
                if (this.Password == null)
                {
                    throw new CryptographicException("Encrypted Rar archive has no password specified.");
                }
                reader.SkipQueue();
                byte[] salt = reader.ReadBytes(8);
                reader.InitializeAes(salt);
            }
            RarHeader header = RarHeader.Create(reader);
            if (header == null)
            {
                return null;
            }
            switch (header.HeaderType)
            {
                case HeaderType.MarkHeader:
                    return header.PromoteHeader<MarkHeader>(reader);

                case HeaderType.ArchiveHeader:
                {
                    ArchiveHeader header2 = header.PromoteHeader<ArchiveHeader>(reader);
                    this.IsEncrypted = header2.HasPassword;
                    return header2;
                }
                case HeaderType.FileHeader:
                    header4 = header.PromoteHeader<FileHeader>(reader);
                    switch (this.StreamingMode)
                    {
                        case SharpCompress.IO.StreamingMode.Streaming:
                        {
                            ReadOnlySubStream actualStream = new ReadOnlySubStream(reader.BaseStream, header4.CompressedSize);
                            if (header4.Salt == null)
                            {
                                header4.PackedStream = actualStream;
                                return header4;
                            }
                            header4.PackedStream = new RarCryptoWrapper(actualStream, this.Password, header4.Salt);
                            return header4;
                        }
                        case SharpCompress.IO.StreamingMode.Seekable:
                        {
                            header4.DataStartPosition = reader.BaseStream.Position;
                            Stream baseStream = reader.BaseStream;
                            baseStream.Position += header4.CompressedSize;
                            return header4;
                        }
                    }
                    throw new InvalidFormatException("Invalid StreamingMode");

                case HeaderType.ProtectHeader:
                {
                    ProtectHeader header3 = header.PromoteHeader<ProtectHeader>(reader);
                    switch (this.StreamingMode)
                    {
                        case SharpCompress.IO.StreamingMode.Streaming:
                            Utility.Skip(reader.BaseStream, (long) header3.DataSize);
                            return header3;

                        case SharpCompress.IO.StreamingMode.Seekable:
                        {
                            Stream stream1 = reader.BaseStream;
                            stream1.Position += header3.DataSize;
                            return header3;
                        }
                    }
                    throw new InvalidFormatException("Invalid StreamingMode");
                }
                case HeaderType.NewSubHeader:
                    header4 = header.PromoteHeader<FileHeader>(reader);
                    switch (this.StreamingMode)
                    {
                        case SharpCompress.IO.StreamingMode.Streaming:
                            Utility.Skip(reader.BaseStream, header4.CompressedSize);
                            return header4;

                        case SharpCompress.IO.StreamingMode.Seekable:
                        {
                            header4.DataStartPosition = reader.BaseStream.Position;
                            Stream stream3 = reader.BaseStream;
                            stream3.Position += header4.CompressedSize;
                            return header4;
                        }
                    }
                    throw new InvalidFormatException("Invalid StreamingMode");

                case HeaderType.EndArchiveHeader:
                    return header.PromoteHeader<EndArchiveHeader>(reader);
            }
            throw new InvalidFormatException("Invalid Rar Header: " + header.HeaderType.ToString());
        }

        internal bool IsEncrypted
        {
            [CompilerGenerated]
            get
            {
                return this.<IsEncrypted>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<IsEncrypted>k__BackingField = value;
            }
        }

        private SharpCompress.Common.Options Options
        {
            [CompilerGenerated]
            get
            {
                return this.<Options>k__BackingField;
            }
            [CompilerGenerated]
            set
            {
                this.<Options>k__BackingField = value;
            }
        }

        public string Password
        {
            [CompilerGenerated]
            get
            {
                return this.<Password>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<Password>k__BackingField = value;
            }
        }

        internal SharpCompress.IO.StreamingMode StreamingMode
        {
            [CompilerGenerated]
            get
            {
                return this.<StreamingMode>k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this.<StreamingMode>k__BackingField = value;
            }
        }

    }
}

