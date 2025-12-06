using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.PPMd;
using SharpCompress.IO;

namespace SharpCompress.Writers.Zip;

public class ZipWriter : AbstractWriter
{
    private readonly CompressionType compressionType;
    private readonly int compressionLevel;
    private readonly List<ZipCentralDirectoryEntry> entries = new();
    private readonly string zipComment;
    private long streamPosition;
    private PpmdProperties? ppmdProps;
    private readonly bool isZip64;
    private readonly string? password;
    private readonly ZipEncryptionType encryptionType;

    public ZipWriter(Stream destination, ZipWriterOptions zipWriterOptions)
        : base(ArchiveType.Zip, zipWriterOptions)
    {
        zipComment = zipWriterOptions.ArchiveComment ?? string.Empty;
        isZip64 = zipWriterOptions.UseZip64;
        if (destination.CanSeek)
        {
            streamPosition = destination.Position;
        }

        compressionType = zipWriterOptions.CompressionType;
        compressionLevel = zipWriterOptions.CompressionLevel;

        // Initialize encryption settings
        password = zipWriterOptions.Password;
        if (!string.IsNullOrEmpty(password))
        {
            // If password is set but encryption type is None, default to AES-256
            encryptionType =
                zipWriterOptions.EncryptionType == ZipEncryptionType.None
                    ? ZipEncryptionType.Aes256
                    : zipWriterOptions.EncryptionType;
        }
        else
        {
            encryptionType = ZipEncryptionType.None;
        }

        if (WriterOptions.LeaveStreamOpen)
        {
            destination = SharpCompressStream.Create(destination, leaveOpen: true);
        }
        InitializeStream(destination);
    }

    private PpmdProperties PpmdProperties => ppmdProps ??= new PpmdProperties();

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing && OutputStream is not null)
        {
            ulong size = 0;
            foreach (var entry in entries)
            {
                size += entry.Write(OutputStream);
            }
            WriteEndRecord(size);
        }
        base.Dispose(isDisposing);
    }

    private static ZipCompressionMethod ToZipCompressionMethod(CompressionType compressionType) =>
        compressionType switch
        {
            CompressionType.None => ZipCompressionMethod.None,
            CompressionType.Deflate => ZipCompressionMethod.Deflate,
            CompressionType.BZip2 => ZipCompressionMethod.BZip2,
            CompressionType.LZMA => ZipCompressionMethod.LZMA,
            CompressionType.PPMd => ZipCompressionMethod.PPMd,
            CompressionType.ZStandard => ZipCompressionMethod.ZStandard,
            _ => throw new InvalidFormatException("Invalid compression method: " + compressionType),
        };

    public override void Write(string entryPath, Stream source, DateTime? modificationTime) =>
        Write(
            entryPath,
            source,
            new ZipWriterEntryOptions() { ModificationDateTime = modificationTime }
        );

    public void Write(string entryPath, Stream source, ZipWriterEntryOptions zipWriterEntryOptions)
    {
        using var output = WriteToStream(entryPath, zipWriterEntryOptions);
        source.CopyTo(output);
    }

    public Stream WriteToStream(string entryPath, ZipWriterEntryOptions options)
    {
        var compression = ToZipCompressionMethod(options.CompressionType ?? compressionType);

        entryPath = NormalizeFilename(entryPath);
        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;

        // Determine the effective encryption type for this entry
        var effectiveEncryption = encryptionType;

        // For WinZip AES, the compression method in the header is set to WinzipAes,
        // and the actual compression method is stored in the extra field
        var headerCompression =
            effectiveEncryption == ZipEncryptionType.Aes128
            || effectiveEncryption == ZipEncryptionType.Aes256
                ? ZipCompressionMethod.WinzipAes
                : compression;

        var entry = new ZipCentralDirectoryEntry(
            headerCompression,
            entryPath,
            (ulong)streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime,
            EncryptionType = effectiveEncryption,
            ActualCompression = compression,
        };

        // Use the archive default setting for zip64 and allow overrides
        var useZip64 = isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        var headersize = (uint)WriteHeader(
            entryPath,
            options,
            entry,
            useZip64,
            effectiveEncryption,
            compression
        );
        streamPosition += headersize;
        return new ZipWritingStream(
            this,
            OutputStream.NotNull(),
            entry,
            compression,
            options.CompressionLevel ?? compressionLevel,
            effectiveEncryption,
            password
        );
    }

    private string NormalizeFilename(string filename)
    {
        filename = filename.Replace('\\', '/');

        var pos = filename.IndexOf(':');
        if (pos >= 0)
        {
            filename = filename.Remove(0, pos + 1);
        }

        return filename.Trim('/');
    }

    private string NormalizeDirectoryName(string directoryName)
    {
        directoryName = NormalizeFilename(directoryName);
        // Ensure directory name ends with '/' for zip format
        if (!string.IsNullOrEmpty(directoryName) && !directoryName.EndsWith('/'))
        {
            directoryName += '/';
        }
        return directoryName;
    }

    public override void WriteDirectory(string directoryName, DateTime? modificationTime)
    {
        var normalizedName = NormalizeDirectoryName(directoryName);
        if (string.IsNullOrEmpty(normalizedName))
        {
            return; // Skip empty or root directory
        }

        var options = new ZipWriterEntryOptions { ModificationDateTime = modificationTime };
        WriteDirectoryEntry(normalizedName, options);
    }

    public override Task WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        // Synchronous implementation is sufficient for directory entries
        WriteDirectory(directoryName, modificationTime);
        return Task.CompletedTask;
    }

    private void WriteDirectoryEntry(string directoryPath, ZipWriterEntryOptions options)
    {
        var compression = ZipCompressionMethod.None;

        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;

        var entry = new ZipCentralDirectoryEntry(
            compression,
            directoryPath,
            (ulong)streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime,
            Crc = 0,
            Compressed = 0,
            Decompressed = 0,
        };

        // Use the archive default setting for zip64 and allow overrides
        var useZip64 = isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        // Directory entries are never encrypted
        var headersize = (uint)WriteHeader(
            directoryPath,
            options,
            entry,
            useZip64,
            ZipEncryptionType.None,
            compression
        );
        streamPosition += headersize;
        entries.Add(entry);
    }

    private int WriteHeader(
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64,
        ZipEncryptionType encryption,
        ZipCompressionMethod actualCompression
    )
    {
        // We err on the side of caution until the zip specification clarifies how to support this
        if (!OutputStream.CanSeek && useZip64)
        {
            throw new NotSupportedException(
                "Zip64 extensions are not supported on non-seekable streams"
            );
        }

        // Encryption is only supported with seekable streams for now
        if (!OutputStream.CanSeek && encryption != ZipEncryptionType.None)
        {
            throw new NotSupportedException("Encryption is not supported on non-seekable streams");
        }

        // Determine the compression method to write in the header
        var headerCompression =
            encryption == ZipEncryptionType.Aes128 || encryption == ZipEncryptionType.Aes256
                ? ZipCompressionMethod.WinzipAes
                : actualCompression;

        var encodedFilename = WriterOptions.ArchiveEncoding.Encode(filename);

        Span<byte> intBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ZipHeaderFactory.ENTRY_HEADER_BYTES);
        OutputStream.Write(intBuf);

        // Determine version needed
        if (encryption == ZipEncryptionType.Aes128 || encryption == ZipEncryptionType.Aes256)
        {
            OutputStream.Write(stackalloc byte[] { 51, 0 }); // WinZip AES requires version 5.1
        }
        else if (actualCompression == ZipCompressionMethod.Deflate)
        {
            if (OutputStream.CanSeek && useZip64)
            {
                OutputStream.Write(stackalloc byte[] { 45, 0 }); //smallest allowed version for zip64
            }
            else
            {
                OutputStream.Write(stackalloc byte[] { 20, 0 }); //older version which is more compatible
            }
        }
        else
        {
            OutputStream.Write(stackalloc byte[] { 63, 0 }); //version says we used PPMd or LZMA
        }

        var flags = Equals(WriterOptions.ArchiveEncoding.GetEncoding(), Encoding.UTF8)
            ? HeaderFlags.Efs
            : HeaderFlags.None;

        // Add encryption flag
        if (encryption != ZipEncryptionType.None)
        {
            flags |= HeaderFlags.Encrypted;
        }

        if (!OutputStream.CanSeek)
        {
            flags |= HeaderFlags.UsePostDataDescriptor;

            if (actualCompression == ZipCompressionMethod.LZMA)
            {
                flags |= HeaderFlags.Bit1; // eos marker
            }
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
        OutputStream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)headerCompression);
        OutputStream.Write(intBuf.Slice(0, 2)); // zipping method
        BinaryPrimitives.WriteUInt32LittleEndian(
            intBuf,
            zipWriterEntryOptions.ModificationDateTime.DateTimeToDosTime()
        );
        OutputStream.Write(intBuf);

        // zipping date and time
        OutputStream.Write(stackalloc byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

        // unused CRC, un/compressed size, updated later
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedFilename.Length);
        OutputStream.Write(intBuf.Slice(0, 2)); // filename length

        // Calculate extra field length
        var extralength = 0;
        if (OutputStream.CanSeek && useZip64)
        {
            extralength += 2 + 2 + 8 + 8; // Zip64 extra field
        }

        // WinZip AES extra field: 2 (id) + 2 (size) + 2 (version) + 2 (vendor) + 1 (strength) + 2 (actual compression)
        if (encryption == ZipEncryptionType.Aes128 || encryption == ZipEncryptionType.Aes256)
        {
            extralength += 2 + 2 + 7;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
        OutputStream.Write(intBuf.Slice(0, 2)); // extra length
        OutputStream.Write(encodedFilename, 0, encodedFilename.Length);

        // Write Zip64 extra field
        if (OutputStream.CanSeek && useZip64)
        {
            OutputStream.Write(new byte[2 + 2 + 8 + 8], 0, 2 + 2 + 8 + 8); // reserve space for zip64 data
            entry.Zip64HeaderOffset = (ushort)(6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length);
        }

        // Write WinZip AES extra field
        if (encryption == ZipEncryptionType.Aes128 || encryption == ZipEncryptionType.Aes256)
        {
            Span<byte> aesExtra = stackalloc byte[11];
            // Extra field ID: 0x9901 (WinZip AES)
            BinaryPrimitives.WriteUInt16LittleEndian(aesExtra, 0x9901);
            // Extra field data size: 7 bytes
            BinaryPrimitives.WriteUInt16LittleEndian(aesExtra.Slice(2), 7);
            // AES encryption version: 2 (AE-2)
            BinaryPrimitives.WriteUInt16LittleEndian(aesExtra.Slice(4), 0x0002);
            // Vendor ID: "AE" = 0x4541
            BinaryPrimitives.WriteUInt16LittleEndian(aesExtra.Slice(6), 0x4541);
            // AES encryption strength: 1=128-bit, 3=256-bit
            aesExtra[8] = encryption == ZipEncryptionType.Aes128 ? (byte)1 : (byte)3;
            // Actual compression method
            BinaryPrimitives.WriteUInt16LittleEndian(aesExtra.Slice(9), (ushort)actualCompression);
            OutputStream.Write(aesExtra);
        }

        return 6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length + extralength;
    }

    private void WriteFooter(uint crc, uint compressed, uint uncompressed)
    {
        Span<byte> intBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, crc);
        OutputStream.Write(intBuf);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, compressed);
        OutputStream.Write(intBuf);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, uncompressed);
        OutputStream.Write(intBuf);
    }

    private void WriteEndRecord(ulong size)
    {
        var zip64EndOfCentralDirectoryNeeded =
            entries.Count > ushort.MaxValue
            || streamPosition >= uint.MaxValue
            || size >= uint.MaxValue;

        var sizevalue = size >= uint.MaxValue ? uint.MaxValue : (uint)size;
        var streampositionvalue =
            streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)streamPosition;

        Span<byte> intBuf = stackalloc byte[8];
        if (zip64EndOfCentralDirectoryNeeded)
        {
            var recordlen = 2 + 2 + 4 + 4 + 8 + 8 + 8 + 8;

            // Write zip64 end of central directory record
            OutputStream.Write(stackalloc byte[] { 80, 75, 6, 6 });

            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)recordlen);
            OutputStream.Write(intBuf); // Size of zip64 end of central directory record
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 45);
            OutputStream.Write(intBuf.Slice(0, 2)); // Made by
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 45);
            OutputStream.Write(intBuf.Slice(0, 2)); // Version needed

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            OutputStream.Write(intBuf.Slice(0, 4)); // Disk number
            OutputStream.Write(intBuf.Slice(0, 4)); // Central dir disk

            // TODO: entries.Count is int, so max 2^31 files
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)entries.Count);
            OutputStream.Write(intBuf); // Entries in this disk
            OutputStream.Write(intBuf); // Total entries
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, size);
            OutputStream.Write(intBuf); // Central Directory size
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)streamPosition);
            OutputStream.Write(intBuf); // Disk offset

            // Write zip64 end of central directory locator
            OutputStream.Write(stackalloc byte[] { 80, 75, 6, 7 });

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            OutputStream.Write(intBuf.Slice(0, 4)); // Entry disk
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)streamPosition + size);
            OutputStream.Write(intBuf); // Offset to the zip64 central directory
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 1);
            OutputStream.Write(intBuf.Slice(0, 4)); // Number of disks

            streamPosition += 4 + 8 + recordlen + (4 + 4 + 8 + 4);
        }

        // Write normal end of central directory record
        OutputStream.Write(stackalloc byte[] { 80, 75, 5, 6, 0, 0, 0, 0 });
        BinaryPrimitives.WriteUInt16LittleEndian(
            intBuf,
            (ushort)(entries.Count < 0xFFFF ? entries.Count : 0xFFFF)
        );
        OutputStream.Write(intBuf.Slice(0, 2));
        OutputStream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, sizevalue);
        OutputStream.Write(intBuf.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, streampositionvalue);
        OutputStream.Write(intBuf.Slice(0, 4));
        var encodedComment = WriterOptions.ArchiveEncoding.Encode(zipComment);
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
        OutputStream.Write(intBuf.Slice(0, 2));
        OutputStream.Write(encodedComment, 0, encodedComment.Length);
    }

    #region Nested type: ZipWritingStream

    internal class ZipWritingStream : Stream
    {
        private readonly CRC32 crc = new();
        private readonly ZipCentralDirectoryEntry entry;
        private readonly Stream originalStream;
        private readonly Stream writeStream;
        private readonly ZipWriter writer;
        private readonly ZipCompressionMethod zipCompressionMethod;
        private readonly int compressionLevel;
        private readonly ZipEncryptionType encryptionType;
        private readonly string? password;
        private SharpCompressStream? counting;
        private Stream? encryptionStream;
        private ulong decompressed;

        // Flag to prevent throwing exceptions on Dispose
        private bool limitsExceeded;
        private bool isDisposed;

        internal ZipWritingStream(
            ZipWriter writer,
            Stream originalStream,
            ZipCentralDirectoryEntry entry,
            ZipCompressionMethod zipCompressionMethod,
            int compressionLevel,
            ZipEncryptionType encryptionType,
            string? password
        )
        {
            this.writer = writer;
            this.originalStream = originalStream;
            this.entry = entry;
            this.zipCompressionMethod = zipCompressionMethod;
            this.compressionLevel = compressionLevel;
            this.encryptionType = encryptionType;
            this.password = password;
            writeStream = GetWriteStream(originalStream);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        private Stream GetWriteStream(Stream writeStream)
        {
            counting = new SharpCompressStream(writeStream, leaveOpen: true);
            Stream output = counting;

            // Wrap with encryption stream if needed
            if (encryptionType == ZipEncryptionType.Aes128)
            {
                encryptionStream = new WinzipAesEncryptionStream(
                    counting,
                    password!,
                    WinzipAesKeySize.KeySize128
                );
                output = encryptionStream;
            }
            else if (encryptionType == ZipEncryptionType.Aes256)
            {
                encryptionStream = new WinzipAesEncryptionStream(
                    counting,
                    password!,
                    WinzipAesKeySize.KeySize256
                );
                output = encryptionStream;
            }
            else if (encryptionType == ZipEncryptionType.PkwareTraditional)
            {
                // For PKWARE traditional encryption, we need to write the encryption header
                // and wrap the stream in the crypto stream
                var encryptor = PkwareTraditionalEncryptionData.ForWrite(
                    password!,
                    writer.WriterOptions.ArchiveEncoding
                );
                // Write the encryption header (12 bytes)
                // CRC is not known yet, so we use 0 for now (it gets verified with time for streaming)
                var header = encryptor.GenerateEncryptionHeader(0, 0);
                counting.Write(header, 0, header.Length);

                encryptionStream = new PkwareTraditionalCryptoStream(
                    new NonDisposingStream(counting),
                    encryptor,
                    CryptoMode.Encrypt
                );
                output = encryptionStream;
            }

            switch (zipCompressionMethod)
            {
                case ZipCompressionMethod.None:
                {
                    return output;
                }
                case ZipCompressionMethod.Deflate:
                {
                    return new DeflateStream(
                        output,
                        CompressionMode.Compress,
                        (CompressionLevel)compressionLevel
                    );
                }
                case ZipCompressionMethod.BZip2:
                {
                    return new BZip2Stream(output, CompressionMode.Compress, false);
                }
                case ZipCompressionMethod.LZMA:
                {
                    // LZMA with encryption is not supported per ZIP spec
                    if (encryptionType != ZipEncryptionType.None)
                    {
                        throw new NotSupportedException(
                            "LZMA compression with encryption is not supported"
                        );
                    }
                    counting.WriteByte(9);
                    counting.WriteByte(20);
                    counting.WriteByte(5);
                    counting.WriteByte(0);

                    var lzmaStream = new LzmaStream(
                        new LzmaEncoderProperties(!originalStream.CanSeek),
                        false,
                        output
                    );
                    counting.Write(lzmaStream.Properties, 0, lzmaStream.Properties.Length);
                    return lzmaStream;
                }
                case ZipCompressionMethod.PPMd:
                {
                    counting.Write(writer.PpmdProperties.Properties, 0, 2);
                    return new PpmdStream(writer.PpmdProperties, output, true);
                }
                case ZipCompressionMethod.ZStandard:
                {
                    return new ZstdSharp.CompressionStream(output, compressionLevel);
                }
                default:
                {
                    throw new NotSupportedException("CompressionMethod: " + zipCompressionMethod);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            base.Dispose(disposing);
            if (disposing)
            {
                writeStream.Dispose();

                // Dispose encryption stream to finalize encryption (e.g., write auth code for AES)
                encryptionStream?.Dispose();

                if (limitsExceeded)
                {
                    // We have written invalid data into the archive,
                    // so we destroy it now, instead of allowing the user to continue
                    // with a defunct archive
                    originalStream.Dispose();
                    return;
                }

                var countingCount = counting?.InternalPosition ?? 0;
                entry.Crc = (uint)crc.Crc32Result;
                entry.Compressed = (ulong)countingCount;
                entry.Decompressed = decompressed;

                var zip64 =
                    entry.Compressed >= uint.MaxValue || entry.Decompressed >= uint.MaxValue;
                var compressedvalue = zip64 ? uint.MaxValue : (uint)countingCount;
                var decompressedvalue = zip64 ? uint.MaxValue : (uint)entry.Decompressed;

                if (originalStream.CanSeek)
                {
                    // Clear UsePostDataDescriptor flag (bit 3) since we're updating sizes in place
                    // But preserve the Encrypted flag (bit 0) if encryption is enabled
                    originalStream.Position = (long)(entry.HeaderOffset + 6);
                    // Only the Encrypted flag should be in the low byte for seekable streams
                    originalStream.WriteByte(
                        encryptionType != ZipEncryptionType.None
                            ? (byte)HeaderFlags.Encrypted
                            : (byte)0
                    );

                    if (
                        countingCount == 0
                        && entry.Decompressed == 0
                        && encryptionType == ZipEncryptionType.None
                    )
                    {
                        // set compression to STORED for zero byte files (no compression data)
                        // But not if encrypted, as encrypted files always have some data
                        originalStream.Position = (long)(entry.HeaderOffset + 8);
                        originalStream.WriteByte(0);
                        originalStream.WriteByte(0);
                    }

                    originalStream.Position = (long)(entry.HeaderOffset + 14);

                    writer.WriteFooter(entry.Crc, compressedvalue, decompressedvalue);

                    // Ideally, we should not throw from Dispose()
                    // We should not get here as the Write call checks the limits
                    if (zip64 && entry.Zip64HeaderOffset == 0)
                    {
                        throw new NotSupportedException(
                            "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                        );
                    }

                    // If we have pre-allocated space for zip64 data,
                    // fill it out, even if it is not required
                    if (entry.Zip64HeaderOffset != 0)
                    {
                        originalStream.Position = (long)(
                            entry.HeaderOffset + entry.Zip64HeaderOffset
                        );
                        Span<byte> intBuf = stackalloc byte[8];
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                        originalStream.Write(intBuf.Slice(0, 2));
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 8 + 8);
                        originalStream.Write(intBuf.Slice(0, 2));

                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, entry.Decompressed);
                        originalStream.Write(intBuf);
                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, entry.Compressed);
                        originalStream.Write(intBuf);
                    }

                    originalStream.Position = writer.streamPosition + (long)entry.Compressed;
                    writer.streamPosition += (long)entry.Compressed;
                }
                else
                {
                    // We have a streaming archive, so we should add a post-data-descriptor,
                    // but we cannot as it does not hold the zip64 values
                    // Throwing an exception until the zip specification is clarified

                    // Ideally, we should not throw from Dispose()
                    // We should not get here as the Write call checks the limits
                    if (zip64)
                    {
                        throw new NotSupportedException(
                            "Streams larger than 4GiB are not supported for non-seekable streams"
                        );
                    }

                    Span<byte> intBuf = stackalloc byte[4];
                    BinaryPrimitives.WriteUInt32LittleEndian(
                        intBuf,
                        ZipHeaderFactory.POST_DATA_DESCRIPTOR
                    );
                    originalStream.Write(intBuf);
                    writer.WriteFooter(entry.Crc, compressedvalue, decompressedvalue);
                    writer.streamPosition += (long)entry.Compressed + 16;
                }
                writer.entries.Add(entry);
            }
        }

        public override void Flush() => writeStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            // We check the limits first, because we can keep the archive consistent
            // if we can prevent the writes from happening
            if (entry.Zip64HeaderOffset == 0)
            {
                var countingCount = counting?.InternalPosition ?? 0;
                // Pre-check, the counting.Count is not exact, as we do not know the size before having actually compressed it
                if (
                    limitsExceeded
                    || ((decompressed + (uint)count) > uint.MaxValue)
                    || (countingCount + (uint)count) > uint.MaxValue
                )
                {
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }
            }

            decompressed += (uint)count;
            crc.SlurpBlock(buffer, offset, count);
            writeStream.Write(buffer, offset, count);

            if (entry.Zip64HeaderOffset == 0)
            {
                var countingCount = counting?.InternalPosition ?? 0;
                // Post-check, this is accurate
                if ((decompressed > uint.MaxValue) || countingCount > uint.MaxValue)
                {
                    // We have written the data, so the archive is now broken
                    // Throwing the exception here, allows us to avoid
                    // throwing an exception in Dispose() which is discouraged
                    // as it can mask other errors
                    limitsExceeded = true;
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }
            }
        }
    }

    /// <summary>
    /// A stream wrapper that doesn't dispose the underlying stream when disposed.
    /// This is used in encryption scenarios where the crypto stream would otherwise
    /// dispose the counting stream prematurely, before we can read the final count.
    /// </summary>
    private class NonDisposingStream : Stream
    {
        private readonly Stream _stream;

        public NonDisposingStream(Stream stream) => _stream = stream;

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;

        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public override void Flush() => _stream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

        public override void SetLength(long value) => _stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _stream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            // Don't dispose the underlying stream
            base.Dispose(disposing);
        }
    }

    #endregion Nested type: ZipWritingStream
}
