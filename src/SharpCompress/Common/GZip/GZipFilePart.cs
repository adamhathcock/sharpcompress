using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Converters;
using SharpCompress.IO;

namespace SharpCompress.Common.GZip
{
    internal class GZipFilePart : FilePart
    {
        private string name;
        private readonly Stream stream;

        internal GZipFilePart(Stream stream)
        {
            ReadAndValidateGzipHeader(stream);
            EntryStartPosition = stream.Position;
            this.stream = stream;
        }

        internal long EntryStartPosition { get; }

        internal DateTime? DateModified { get; private set; }

        internal override string FilePartName { get { return name; } }

        internal override Stream GetCompressedStream()
        {
            return new DeflateStream(stream, CompressionMode.Decompress, CompressionLevel.Default, false);
        }

        internal override Stream GetRawStream()
        {
            return stream;
        }

        private void ReadAndValidateGzipHeader(Stream stream)
        {
            // read the header on the first read
            using (var header = ByteArrayPool.RentScope(10))
            {
                int n = stream.Read(header);

                // workitem 8501: handle edge case (decompress empty stream)
                if (n == 0)
                {
                    return;
                }

                if (n != 10)
                {
                    throw new ZlibException("Not a valid GZIP stream.");
                }

                if (header[0] != 0x1F || header[1] != 0x8B || header[2] != 8)
                {
                    throw new ZlibException("Bad GZIP header.");
                }

                Int32 timet = DataConverter.LittleEndian.GetInt32(header.Array, 4);
                DateModified = TarHeader.Epoch.AddSeconds(timet);
                if ((header[3] & 0x04) == 0x04)
                {
                    // read and discard extra field
                    n = stream.Read(header.Array, 0, 2); // 2-byte length field

                    Int16 extraLength = (Int16)(header[0] + header[1] * 256);
                    using (var extra = ByteArrayPool.RentScope(extraLength))
                    {
                        n = stream.Read(extra);
                        if (n != extraLength)
                        {
                            throw new ZlibException("Unexpected end-of-file reading GZIP header.");
                        }
                    }
                }
                if ((header[3] & 0x08) == 0x08)
                {
                    name = ReadZeroTerminatedString(stream);
                }
                if ((header[3] & 0x10) == 0x010)
                {
                    ReadZeroTerminatedString(stream);
                }
                if ((header[3] & 0x02) == 0x02)
                {
                    stream.ReadByte(); // CRC16, ignore
                }
            }
        }

        private static string ReadZeroTerminatedString(Stream stream)
        {
            using (var buf1 = ByteArrayPool.RentScope(1))
            {
                var list = new List<byte>();
                bool done = false;
                do
                {
                    // workitem 7740
                    int n = stream.Read(buf1);
                    if (n != 1)
                    {
                        throw new ZlibException("Unexpected EOF reading GZIP header.");
                    }
                    if (buf1[0] == 0)
                    {
                        done = true;
                    }
                    else
                    {
                        list.Add(buf1[0]);
                    }
                }
                while (!done);
                byte[] a = list.ToArray();
                return ArchiveEncoding.Default.GetString(a, 0, a.Length);
            }
        }
    }
}