namespace SharpCompress.Common.GZip
{
    using SharpCompress.Common;
    using SharpCompress.Common.Tar.Headers;
    using SharpCompress.Compressor;
    using SharpCompress.Compressor.Deflate;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class GZipFilePart : FilePart
    {
        [CompilerGenerated]
        private DateTime? _DateModified_k__BackingField;
        private string name;
        private readonly Stream stream;

        internal GZipFilePart(Stream stream)
        {
            this.ReadAndValidateGzipHeader(stream);
            this.stream = stream;
        }

        internal override Stream GetCompressedStream()
        {
            return new DeflateStream(this.stream, CompressionMode.Decompress, CompressionLevel.Default, false);
        }

        internal override Stream GetRawStream()
        {
            return this.stream;
        }

        private void ReadAndValidateGzipHeader(Stream stream)
        {
            byte[] buffer = new byte[10];
            int num = stream.Read(buffer, 0, buffer.Length);
            if (num != 0)
            {
                if (num != 10)
                {
                    throw new ZlibException("Not a valid GZIP stream.");
                }
                if (((buffer[0] != 0x1f) || (buffer[1] != 0x8b)) || (buffer[2] != 8))
                {
                    throw new ZlibException("Bad GZIP header.");
                }
                int num2 = BitConverter.ToInt32(buffer, 4);
                this.DateModified = new DateTime?(TarHeader.Epoch.AddSeconds((double) num2));
                if ((buffer[3] & 4) == 4)
                {
                    num = stream.Read(buffer, 0, 2);
                    short num3 = (short) (buffer[0] + (buffer[1] * 0x100));
                    byte[] buffer2 = new byte[num3];
                    if (stream.Read(buffer2, 0, buffer2.Length) != num3)
                    {
                        throw new ZlibException("Unexpected end-of-file reading GZIP header.");
                    }
                }
                if ((buffer[3] & 8) == 8)
                {
                    this.name = ReadZeroTerminatedString(stream);
                }
                if ((buffer[3] & 0x10) == 0x10)
                {
                    ReadZeroTerminatedString(stream);
                }
                if ((buffer[3] & 2) == 2)
                {
                    stream.ReadByte();
                }
            }
        }

        private static string ReadZeroTerminatedString(Stream stream)
        {
            byte[] buffer = new byte[1];
            List<byte> list = new List<byte>();
            bool flag = false;
            do
            {
                if (stream.Read(buffer, 0, 1) != 1)
                {
                    throw new ZlibException("Unexpected EOF reading GZIP header.");
                }
                if (buffer[0] == 0)
                {
                    flag = true;
                }
                else
                {
                    list.Add(buffer[0]);
                }
            }
            while (!flag);
            byte[] bytes = list.ToArray();
            return ArchiveEncoding.Default.GetString(bytes, 0, bytes.Length);
        }

        internal DateTime? DateModified
        {
            [CompilerGenerated]
            get
            {
                return this._DateModified_k__BackingField;
            }
            [CompilerGenerated]
            private set
            {
                this._DateModified_k__BackingField = value;
            }
        }

        internal override string FilePartName
        {
            get
            {
                return this.name;
            }
        }
    }
}

