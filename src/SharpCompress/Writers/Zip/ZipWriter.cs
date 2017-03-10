using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.PPMd;
using SharpCompress.Converters;
using SharpCompress.IO;

namespace SharpCompress.Writers.Zip
{
    public class ZipWriter : AbstractWriter
    {
        private readonly CompressionType compressionType;
        private readonly CompressionLevel compressionLevel;
        private readonly List<ZipCentralDirectoryEntry> entries = new List<ZipCentralDirectoryEntry>();
        private readonly string zipComment;
        private long streamPosition;
        private PpmdProperties ppmdProps;
        private bool isZip64;

        public ZipWriter(Stream destination, ZipWriterOptions zipWriterOptions)
            : base(ArchiveType.Zip)
        {
            zipComment = zipWriterOptions.ArchiveComment ?? string.Empty;
            isZip64 = zipWriterOptions.UseZip64;

            compressionType = zipWriterOptions.CompressionType;
            compressionLevel = zipWriterOptions.DeflateCompressionLevel;
            InitalizeStream(destination, !zipWriterOptions.LeaveStreamOpen);
        }

        private PpmdProperties PpmdProperties
        {
            get
            {
                if (ppmdProps == null)
                {
                    ppmdProps = new PpmdProperties();
                }
                return ppmdProps;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                ulong size = 0;
                foreach (ZipCentralDirectoryEntry entry in entries)
                {
                    size += entry.Write(OutputStream, ToZipCompressionMethod(compressionType));
                }
                WriteEndRecord(size);
            }
            base.Dispose(isDisposing);
        }
        private static ZipCompressionMethod ToZipCompressionMethod(CompressionType compressionType)
        {
            switch (compressionType)
            {
                case CompressionType.None:
                    {
                        return ZipCompressionMethod.None;
                    }
                case CompressionType.Deflate:
                    {
                        return ZipCompressionMethod.Deflate;
                    }
                case CompressionType.BZip2:
                    {
                        return ZipCompressionMethod.BZip2;
                    }
                case CompressionType.LZMA:
                    {
                        return ZipCompressionMethod.LZMA;
                    }
                case CompressionType.PPMd:
                    {
                        return ZipCompressionMethod.PPMd;
                    }
                default:
                    throw new InvalidFormatException("Invalid compression method: " + compressionType);
            }
        }

        public override void Write(string entryPath, Stream source, DateTime? modificationTime)
        {
            Write(entryPath, source, new ZipWriterEntryOptions()
                                     {
                                         ModificationDateTime =  modificationTime
                                     });
        }

        public void Write(string entryPath, Stream source, ZipWriterEntryOptions zipWriterEntryOptions)
        {
            using (Stream output = WriteToStream(entryPath, zipWriterEntryOptions))
            {
                source.TransferTo(output);
            }
        }

        public Stream WriteToStream(string entryPath, ZipWriterEntryOptions options)
        {
            entryPath = NormalizeFilename(entryPath);
            options.ModificationDateTime = options.ModificationDateTime ?? DateTime.Now;
            options.EntryComment = options.EntryComment ?? string.Empty;
            var entry = new ZipCentralDirectoryEntry
                        {
                            Comment = options.EntryComment,
                            FileName = entryPath,
                            ModificationTime = options.ModificationDateTime,
                            HeaderOffset = (uint)streamPosition
                        };

            // Switch to allocating space for zip64, if the archive is larger than 2GB
            var useZip64 = (OutputStream.CanSeek && OutputStream.Length > int.MaxValue) || isZip64;

            // Allow direct disabling
            if (options.EnableZip64.HasValue)
                useZip64 = options.EnableZip64.Value;

            var headersize = (uint)WriteHeader(entryPath, options, entry, useZip64);
            streamPosition += headersize;
            return new ZipWritingStream(this, OutputStream, entry, 
                ToZipCompressionMethod(options.CompressionType ?? compressionType), 
                options.DeflateCompressionLevel ?? compressionLevel);
        }

        private string NormalizeFilename(string filename)
        {
            filename = filename.Replace('\\', '/');

            int pos = filename.IndexOf(':');
            if (pos >= 0)
            {
                filename = filename.Remove(0, pos + 1);
            }

            return filename.Trim('/');
        }

        private int WriteHeader(string filename, ZipWriterEntryOptions zipWriterEntryOptions, ZipCentralDirectoryEntry entry, bool useZip64)
        {
            var explicitZipCompressionInfo = ToZipCompressionMethod(zipWriterEntryOptions.CompressionType ?? compressionType);
            byte[] encodedFilename = ArchiveEncoding.Default.GetBytes(filename);

            OutputStream.Write(DataConverter.LittleEndian.GetBytes(ZipHeaderFactory.ENTRY_HEADER_BYTES), 0, 4);
            if (explicitZipCompressionInfo == ZipCompressionMethod.Deflate)
            {
                if (OutputStream.CanSeek && useZip64)
                    OutputStream.Write(new byte[] { 45, 0 }, 0, 2); //smallest allowed version for zip64
                else
                    OutputStream.Write(new byte[] { 20, 0 }, 0, 2); //older version which is more compatible 
            }
            else
            {
                OutputStream.Write(new byte[] { 63, 0 }, 0, 2); //version says we used PPMd or LZMA
            }
            HeaderFlags flags = ArchiveEncoding.Default == Encoding.UTF8 ? HeaderFlags.UTF8 : 0;
            if (!OutputStream.CanSeek)
            {
                // We cannot really use post data with zip64, but we have nothing else
                flags |= HeaderFlags.UsePostDataDescriptor;
                
                if (explicitZipCompressionInfo == ZipCompressionMethod.LZMA)
                {
                    flags |= HeaderFlags.Bit1; // eos marker
                }
            }
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)flags), 0, 2);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)explicitZipCompressionInfo), 0, 2); // zipping method
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(zipWriterEntryOptions.ModificationDateTime.DateTimeToDosTime()), 0, 4);

            // zipping date and time
            OutputStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 12);

            // unused CRC, un/compressed size, updated later
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)encodedFilename.Length), 0, 2); // filename length

            var extralength = 0;
            if (OutputStream.CanSeek && useZip64)
                extralength = 2 + 2 + 8 + 8;

            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)extralength), 0, 2); // extra length
            OutputStream.Write(encodedFilename, 0, encodedFilename.Length);

            if (extralength != 0)
            {
                OutputStream.Write(new byte[extralength], 0, extralength); // reserve space for zip64 data
                entry.Zip64HeaderOffset = (ushort)(6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length);
            }

            return 6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length + extralength;
        }

        private void WriteFooter(uint crc, uint compressed, uint uncompressed)
        {
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(crc), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(compressed), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(uncompressed), 0, 4);
        }

        private void WritePostdataDescriptor(uint crc, ulong compressed, ulong uncompressed)
        {
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(ZipHeaderFactory.POST_DATA_DESCRIPTOR), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(crc), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((uint)compressed), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((uint)uncompressed), 0, 4);
        }

        private void WriteEndRecord(ulong size)
        {
            byte[] encodedComment = ArchiveEncoding.Default.GetBytes(zipComment);
            var zip64 = isZip64 || entries.Count > ushort.MaxValue || streamPosition >= uint.MaxValue || size >= uint.MaxValue;

            var sizevalue = size >= uint.MaxValue ? uint.MaxValue : (uint)size;
            var streampositionvalue = streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)streamPosition; 

            if (zip64)
            {
                var recordlen = 2 + 2 + 4 + 4 + 8 + 8 + 8 + 8;

                // Write zip64 end of central directory record
                OutputStream.Write(new byte[] { 80, 75, 6, 6 }, 0, 4);
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((ulong)recordlen), 0, 8); // Size of zip64 end of central directory record
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)0), 0, 2); // Made by
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)45), 0, 2); // Version needed

                OutputStream.Write(DataConverter.LittleEndian.GetBytes((uint)0), 0, 4); // Disk number
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((uint)0), 0, 4); // Central dir disk

                // TODO: entries.Count is int, so max 2^31 files
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((ulong)entries.Count), 0, 8); // Entries in this disk
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((ulong)entries.Count), 0, 8); // Total entries
                OutputStream.Write(DataConverter.LittleEndian.GetBytes(size), 0, 8); // Central Directory size
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((ulong)streamPosition), 0, 8); // Disk offset

                // Write zip64 end of central directory locator
                OutputStream.Write(new byte[] { 80, 75, 6, 7 }, 0, 4);

                OutputStream.Write(DataConverter.LittleEndian.GetBytes(0uL), 0, 4); // Entry disk
                OutputStream.Write(DataConverter.LittleEndian.GetBytes((ulong)streamPosition + size), 0, 8); // Offset to the zip64 central directory
                OutputStream.Write(DataConverter.LittleEndian.GetBytes(0u), 0, 4); // Number of disks

                streamPosition += recordlen + (4 + 4 + 8 + 4);
                streampositionvalue = streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)streampositionvalue;
            }

            // Write normal end of central directory record
            OutputStream.Write(new byte[] {80, 75, 5, 6, 0, 0, 0, 0}, 0, 8);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)entries.Count), 0, 2);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)entries.Count), 0, 2);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes(sizevalue), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((uint)streampositionvalue), 0, 4);
            OutputStream.Write(DataConverter.LittleEndian.GetBytes((ushort)encodedComment.Length), 0, 2);
            OutputStream.Write(encodedComment, 0, encodedComment.Length);
        }

        #region Nested type: ZipWritingStream

        internal class ZipWritingStream : Stream
        {
            private readonly CRC32 crc = new CRC32();
            private readonly ZipCentralDirectoryEntry entry;
            private readonly Stream originalStream;
            private readonly Stream writeStream;
            private readonly ZipWriter writer;
            private readonly ZipCompressionMethod zipCompressionMethod;
            private readonly CompressionLevel compressionLevel;
            private CountingWritableSubStream counting;
            private ulong decompressed;

            internal ZipWritingStream(ZipWriter writer, Stream originalStream, ZipCentralDirectoryEntry entry, 
                ZipCompressionMethod zipCompressionMethod, CompressionLevel compressionLevel)
            {
                this.writer = writer;
                this.originalStream = originalStream;
                this.writer = writer;
                this.entry = entry;
                this.zipCompressionMethod = zipCompressionMethod;
                this.compressionLevel = compressionLevel;
                writeStream = GetWriteStream(originalStream);
            }

            public override bool CanRead { get { return false; } }

            public override bool CanSeek { get { return false; } }

            public override bool CanWrite { get { return true; } }

            public override long Length { get { throw new NotSupportedException(); } }

            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }

            private Stream GetWriteStream(Stream writeStream)
            {
                counting = new CountingWritableSubStream(writeStream);
                Stream output = counting;
                switch (zipCompressionMethod)
                {
                    case ZipCompressionMethod.None:
                    {
                        return output;
                    }
                    case ZipCompressionMethod.Deflate:
                    {
                        return new DeflateStream(counting, CompressionMode.Compress, compressionLevel,
                                                 true);
                    }
                    case ZipCompressionMethod.BZip2:
                    {
                        return new BZip2Stream(counting, CompressionMode.Compress, true);
                    }
                    case ZipCompressionMethod.LZMA:
                    {
                        counting.WriteByte(9);
                        counting.WriteByte(20);
                        counting.WriteByte(5);
                        counting.WriteByte(0);

                        LzmaStream lzmaStream = new LzmaStream(new LzmaEncoderProperties(!originalStream.CanSeek),
                                                               false, counting);
                        counting.Write(lzmaStream.Properties, 0, lzmaStream.Properties.Length);
                        return lzmaStream;
                    }
                    case ZipCompressionMethod.PPMd:
                    {
                        counting.Write(writer.PpmdProperties.Properties, 0, 2);
                        return new PpmdStream(writer.PpmdProperties, counting, true);
                    }
                    default:
                    {
                        throw new NotSupportedException("CompressionMethod: " + zipCompressionMethod);
                    }
                }
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing)
                {
                    writeStream.Dispose();
                    entry.Crc = (uint)crc.Crc32Result;
                    entry.Compressed = counting.Count;
                    entry.Decompressed = decompressed;

                    var zip64 = entry.Compressed >= uint.MaxValue || entry.Decompressed >= uint.MaxValue || entry.HeaderOffset >= uint.MaxValue;

                    writer.isZip64 |= zip64;

                    var compressedvalue = zip64 ? uint.MaxValue : (uint)counting.Count;
                    var decompressedvalue = zip64 ? uint.MaxValue : (uint)entry.Decompressed;

                    if (originalStream.CanSeek)
                    {
                        originalStream.Position = (long)(entry.HeaderOffset + 6);
                        originalStream.WriteByte(0);
                        
                        originalStream.Position = (long)(entry.HeaderOffset + 14);

                        writer.WriteFooter(entry.Crc, compressedvalue, decompressedvalue);

                        // If we have pre-allocated space for zip64 data, fill it out
                        if (entry.Zip64HeaderOffset != 0)
                        {
                            originalStream.Position = (long)(entry.HeaderOffset + entry.Zip64HeaderOffset);
                            originalStream.Write(DataConverter.LittleEndian.GetBytes((ushort)0x0001), 0, 2);
                            originalStream.Write(DataConverter.LittleEndian.GetBytes((ushort)(8 + 8)), 0, 2);

                            originalStream.Write(DataConverter.LittleEndian.GetBytes(entry.Decompressed), 0, 8);
                            originalStream.Write(DataConverter.LittleEndian.GetBytes(entry.Compressed), 0, 8);
                        }

                        originalStream.Position = writer.streamPosition + (long)entry.Compressed;
                        writer.streamPosition += (long)entry.Compressed;

                    }
                    else
                    {
                        // Bit unclear what happens here, with zip64
                        // We have a streaming archive, so we should add a post-data-descriptor,
                        // but we cannot as it does not hold the zip64 values

                        // The current implementation writes 0xffffffff in the fields here, and the 
                        // the central directory has the extra data required if the fields are overflown
                        originalStream.Write(DataConverter.LittleEndian.GetBytes(ZipHeaderFactory.POST_DATA_DESCRIPTOR), 0, 4);
                        writer.WriteFooter(entry.Crc, 
                                           (uint)(counting.Count >= uint.MaxValue ? uint.MaxValue : counting.Count),
                                           (uint)(entry.Decompressed >= uint.MaxValue ? uint.MaxValue : entry.Decompressed));
                        writer.streamPosition += (long)entry.Compressed + 16;
                    }
                    writer.entries.Add(entry);
                }
            }

            public override void Flush()
            {
                writeStream.Flush();
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
                decompressed += (uint)count;
                crc.SlurpBlock(buffer, offset, count);
                writeStream.Write(buffer, offset, count);
            }
        }

        #endregion
    }
}