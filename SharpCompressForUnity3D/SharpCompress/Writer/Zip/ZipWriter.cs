namespace SharpCompress.Writer.Zip
{
    using SharpCompress;
    using SharpCompress.Common;
    using SharpCompress.Common.Zip;
    using SharpCompress.Common.Zip.Headers;
    using SharpCompress.Compressor;
    using SharpCompress.Compressor.BZip2;
    using SharpCompress.Compressor.Deflate;
    using SharpCompress.Compressor.LZMA;
    using SharpCompress.Compressor.PPMd;
    using SharpCompress.IO;
    using SharpCompress.Writer;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    public class ZipWriter : AbstractWriter
    {
        private readonly List<ZipCentralDirectoryEntry> entries;
        private readonly PpmdProperties ppmdProperties;
        private long streamPosition;
        private readonly string zipComment;
        private readonly ZipCompressionInfo zipCompressionInfo;

        public ZipWriter(Stream destination, CompressionInfo compressionInfo, string zipComment) : base(ArchiveType.Zip)
        {
            this.ppmdProperties = new PpmdProperties();
            this.entries = new List<ZipCentralDirectoryEntry>();
            this.zipComment = zipComment ?? string.Empty;
            this.zipCompressionInfo = new ZipCompressionInfo(compressionInfo);
            base.InitalizeStream(destination, false);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                uint size = 0;
                foreach (ZipCentralDirectoryEntry entry in this.entries)
                {
                    size += entry.Write(base.OutputStream, this.zipCompressionInfo.Compression);
                }
                this.WriteEndRecord(size);
            }
            base.Dispose(isDisposing);
        }

        private string NormalizeFilename(string filename)
        {
            filename = filename.Replace('\\', '/');
            int index = filename.IndexOf(':');
            if (index >= 0)
            {
                filename = filename.Remove(0, index + 1);
            }
            return filename.Trim(new char[] { '/' });
        }

        public override void Write(string entryPath, Stream source, DateTime? modificationTime)
        {
            this.Write(entryPath, source, modificationTime, null, null);
        }
        public void Write(string entryPath, Stream source, DateTime? modificationTime, string comment) { 
            Write( entryPath,  source,  modificationTime,  comment,null);
        }
        public void Write(string entryPath, Stream source, DateTime? modificationTime, string comment,  CompressionInfo compressionInfo)
        {
            using (Stream stream = this.WriteToStream(entryPath, modificationTime, comment, compressionInfo))
            {
                Utility.TransferTo(source, stream);
            }
        }

        private void WriteEndRecord(uint size)
        {
            byte[] bytes = ArchiveEncoding.Default.GetBytes(this.zipComment);
            base.OutputStream.Write(new byte[] { 80, 0x4b, 5, 6, 0, 0, 0, 0 }, 0, 8);
            base.OutputStream.Write(BitConverter.GetBytes((ushort) this.entries.Count), 0, 2);
            base.OutputStream.Write(BitConverter.GetBytes((ushort) this.entries.Count), 0, 2);
            base.OutputStream.Write(BitConverter.GetBytes(size), 0, 4);
            base.OutputStream.Write(BitConverter.GetBytes((uint) this.streamPosition), 0, 4);
            base.OutputStream.Write(BitConverter.GetBytes((ushort) bytes.Length), 0, 2);
            base.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private void WriteFooter(uint crc, uint compressed, uint uncompressed)
        {
            base.OutputStream.Write(BitConverter.GetBytes(crc), 0, 4);
            base.OutputStream.Write(BitConverter.GetBytes(compressed), 0, 4);
            base.OutputStream.Write(BitConverter.GetBytes(uncompressed), 0, 4);
        }
        private int WriteHeader(string filename, DateTime? modificationTime) {
            return WriteHeader(filename, modificationTime, null);
        }
        private int WriteHeader(string filename, DateTime? modificationTime,  CompressionInfo compressionInfo)
        {
            ZipCompressionInfo info = (compressionInfo != null) ? new ZipCompressionInfo(compressionInfo) : this.zipCompressionInfo;
            byte[] bytes = ArchiveEncoding.Default.GetBytes(filename);
            base.OutputStream.Write(BitConverter.GetBytes((uint) 0x4034b50), 0, 4);
            byte[] buffer = new byte[2];
            buffer[0] = 0x3f;
            base.OutputStream.Write(buffer, 0, 2);
            HeaderFlags flags = (ArchiveEncoding.Default == Encoding.UTF8) ? HeaderFlags.UTF8 : ((HeaderFlags) 0);
            if (!base.OutputStream.CanSeek)
            {
                flags = (HeaderFlags) ((ushort) (flags | HeaderFlags.UsePostDataDescriptor));
                if (info.Compression == ZipCompressionMethod.LZMA)
                {
                    flags = (HeaderFlags) ((ushort) (flags | HeaderFlags.Bit1));
                }
            }
            base.OutputStream.Write(BitConverter.GetBytes((ushort) flags), 0, 2);
            base.OutputStream.Write(BitConverter.GetBytes((ushort) info.Compression), 0, 2);
            base.OutputStream.Write(BitConverter.GetBytes(Utility.DateTimeToDosTime(modificationTime)), 0, 4);
            buffer = new byte[12];
            base.OutputStream.Write(buffer, 0, 12);
            base.OutputStream.Write(BitConverter.GetBytes((ushort) bytes.Length), 0, 2);
            base.OutputStream.Write(BitConverter.GetBytes((ushort) 0), 0, 2);
            base.OutputStream.Write(bytes, 0, bytes.Length);
            return (30 + bytes.Length);
        }
        public Stream WriteToStream(string entryPath, DateTime? modificationTime, string comment) {
            return WriteToStream(entryPath, modificationTime, comment, null);
        }
        public Stream WriteToStream(string entryPath, DateTime? modificationTime, string comment,  CompressionInfo compressionInfo)
        {
            entryPath = this.NormalizeFilename(entryPath);
            DateTime? nullable = modificationTime;
            modificationTime = new DateTime?(nullable.HasValue ? nullable.GetValueOrDefault() : DateTime.Now);
            comment = comment ?? "";
            ZipCentralDirectoryEntry entry2 = new ZipCentralDirectoryEntry();
            entry2.Comment = comment;
            entry2.FileName = entryPath;
            entry2.ModificationTime = modificationTime;
            entry2.HeaderOffset = (uint) this.streamPosition;
            ZipCentralDirectoryEntry entry = entry2;
            uint num = (uint) this.WriteHeader(entryPath, modificationTime, compressionInfo);
            this.streamPosition += num;
            return new ZipWritingStream(this, base.OutputStream, entry);
        }

        internal class ZipWritingStream : Stream
        {
            private CountingWritableSubStream counting;
            private readonly CRC32 crc = new CRC32();
            private uint decompressed;
            private readonly ZipCentralDirectoryEntry entry;
            private readonly Stream originalStream;
            private readonly ZipWriter writer;
            private readonly Stream writeStream;

            internal ZipWritingStream(ZipWriter writer, Stream originalStream, ZipCentralDirectoryEntry entry)
            {
                this.writer = writer;
                this.originalStream = originalStream;
                this.writeStream = this.GetWriteStream(originalStream);
                this.writer = writer;
                this.entry = entry;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    this.writeStream.Dispose();
                    this.entry.Crc = (uint) this.crc.Crc32Result;
                    this.entry.Compressed = this.counting.Count;
                    this.entry.Decompressed = this.decompressed;
                    if (this.originalStream.CanSeek)
                    {
                        this.originalStream.Position = this.entry.HeaderOffset + 6;
                        this.originalStream.WriteByte(0);
                        this.originalStream.Position = this.entry.HeaderOffset + 14;
                        this.writer.WriteFooter(this.entry.Crc, this.counting.Count, this.decompressed);
                        this.originalStream.Position = this.writer.streamPosition + this.entry.Compressed;
                        this.writer.streamPosition += this.entry.Compressed;
                    }
                    else
                    {
                        this.originalStream.Write(BitConverter.GetBytes((uint) 0x8074b50), 0, 4);
                        this.writer.WriteFooter(this.entry.Crc, this.counting.Count, this.decompressed);
                        this.writer.streamPosition += this.entry.Compressed + 0x10;
                    }
                    this.writer.entries.Add(this.entry);
                }
            }

            public override void Flush()
            {
                this.writeStream.Flush();
            }

            private Stream GetWriteStream(Stream writeStream)
            {
                this.counting = new CountingWritableSubStream(writeStream);
                Stream counting = this.counting;
                switch (this.writer.zipCompressionInfo.Compression)
                {
                    case ZipCompressionMethod.BZip2:
                        return new BZip2Stream(this.counting, CompressionMode.Compress, true, false);

                    case ZipCompressionMethod.LZMA:
                    {
                        this.counting.WriteByte(9);
                        this.counting.WriteByte(20);
                        this.counting.WriteByte(5);
                        this.counting.WriteByte(0);
                        LzmaStream stream2 = new LzmaStream(new LzmaEncoderProperties(!this.originalStream.CanSeek), false, this.counting);
                        this.counting.Write(stream2.Properties, 0, stream2.Properties.Length);
                        return stream2;
                    }
                    case ZipCompressionMethod.PPMd:
                        this.counting.Write(this.writer.ppmdProperties.Properties, 0, 2);
                        return new PpmdStream(this.writer.ppmdProperties, this.counting, true);

                    case ZipCompressionMethod.None:
                        return counting;

                    case ZipCompressionMethod.Deflate:
                        return new DeflateStream(this.counting, CompressionMode.Compress, this.writer.zipCompressionInfo.DeflateCompressionLevel, true);
                }
                throw new NotSupportedException("CompressionMethod: " + this.writer.zipCompressionInfo.Compression);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.decompressed += (uint) count;
                this.crc.SlurpBlock(buffer, offset, count);
                this.writeStream.Write(buffer, offset, count);
            }

            public override bool CanRead
            {
                get
                {
                    return false;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return false;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return true;
                }
            }

            public override long Length
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public override long Position
            {
                get
                {
                    throw new NotSupportedException();
                }
                set
                {
                    throw new NotSupportedException();
                }
            }
        }
    }
}

