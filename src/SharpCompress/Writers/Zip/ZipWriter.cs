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
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;
using SharpCompress.Providers;
using Constants = SharpCompress.Common.Constants;

namespace SharpCompress.Writers.Zip;

public partial class ZipWriter : AbstractWriter
{
    private readonly CompressionType compressionType;
    private readonly int compressionLevel;
    private readonly List<ZipCentralDirectoryEntry> entries = new();
    private readonly string zipComment;
    private long streamPosition;
    private PpmdProperties? ppmdProps;
    private readonly bool isZip64;

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

        if (WriterOptions.LeaveStreamOpen)
        {
            destination = SharpCompressStream.CreateNonDisposing(destination);
        }
        InitializeStream(destination);
    }

    private PpmdProperties PpmdProperties => ppmdProps ??= new PpmdProperties();

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing && !_isDisposed)
        {
            ulong size = 0;
            foreach (var entry in entries)
            {
                size += entry.Write(OutputStream.NotNull());
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

    public override void Write(string filename, Stream source, DateTime? modificationTime) =>
        Write(
            filename,
            source,
            new ZipWriterEntryOptions() { ModificationDateTime = modificationTime }
        );

    public void Write(string entryPath, Stream source, ZipWriterEntryOptions zipWriterEntryOptions)
    {
        using var output = WriteToStream(entryPath, zipWriterEntryOptions);
        var progressStream = WrapWithProgress(source, entryPath);
        progressStream.CopyTo(output, Constants.BufferSize);
    }

    public Stream WriteToStream(string entryPath, ZipWriterEntryOptions options)
    {
        options.ValidateWithFallback(compressionType, compressionLevel);
        var compression = ToZipCompressionMethod(options.CompressionType ?? compressionType);

        entryPath = NormalizeFilename(entryPath);
        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;
        var entry = new ZipCentralDirectoryEntry(
            compression,
            entryPath,
            (ulong)streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime,
        };

        // Use the archive default setting for zip64 and allow overrides
        var useZip64 = isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        var headersize = (uint)WriteHeader(entryPath, options, entry, useZip64);
        streamPosition += headersize;
        return new ZipWritingStream(
            this,
            OutputStream.NotNull(),
            entry,
            compression,
            options.CompressionLevel ?? compressionLevel
        );
    }

    private string NormalizeFilename(string filename)
    {
        filename = filename.Replace('\\', '/');

#if LEGACY_DOTNET
        var pos = filename.IndexOf(':');
#else
        var pos = filename.IndexOf(':', StringComparison.Ordinal);
#endif
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

    // WriteDirectoryAsync moved to ZipWriter.Async.cs

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

        var headersize = (uint)WriteHeader(directoryPath, options, entry, useZip64);
        streamPosition += headersize;
        entries.Add(entry);
    }

    private int WriteHeader(
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64
    ) => WriteHeader(OutputStream.NotNull(), filename, zipWriterEntryOptions, entry, useZip64);

    private int WriteHeader(
        Stream stream,
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64
    )
    {
        // We err on the side of caution until the zip specification clarifies how to support this
        if (!stream.CanSeek && useZip64)
        {
            throw new NotSupportedException(
                "Zip64 extensions are not supported on non-seekable streams"
            );
        }

        var explicitZipCompressionInfo = ToZipCompressionMethod(
            zipWriterEntryOptions.CompressionType ?? compressionType
        );
        var encodedFilename = WriterOptions.ArchiveEncoding.Encode(filename);

        Span<byte> intBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ZipHeaderFactory.ENTRY_HEADER_BYTES);
        stream.Write(intBuf);
        if (explicitZipCompressionInfo == ZipCompressionMethod.Deflate)
        {
            if (stream.CanSeek && useZip64)
            {
                stream.Write(stackalloc byte[] { 45, 0 }); //smallest allowed version for zip64
            }
            else
            {
                stream.Write(stackalloc byte[] { 20, 0 }); //older version which is more compatible
            }
        }
        else
        {
            stream.Write(stackalloc byte[] { 63, 0 }); //version says we used PPMd or LZMA
        }
        var flags = Equals(WriterOptions.ArchiveEncoding.GetEncoding(), Encoding.UTF8)
            ? HeaderFlags.Efs
            : 0;
        if (!stream.CanSeek)
        {
            flags |= HeaderFlags.UsePostDataDescriptor;

            if (explicitZipCompressionInfo == ZipCompressionMethod.LZMA)
            {
                flags |= HeaderFlags.Bit1; // eos marker
            }
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
        stream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)explicitZipCompressionInfo);
        stream.Write(intBuf.Slice(0, 2)); // zipping method
        BinaryPrimitives.WriteUInt32LittleEndian(
            intBuf,
            zipWriterEntryOptions.ModificationDateTime.DateTimeToDosTime()
        );
        stream.Write(intBuf);

        // zipping date and time
        stream.Write(stackalloc byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });

        // unused CRC, un/compressed size, updated later
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedFilename.Length);
        stream.Write(intBuf.Slice(0, 2)); // filename length

        var extralength = 0;
        if (stream.CanSeek && useZip64)
        {
            extralength = 2 + 2 + 8 + 8;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
        stream.Write(intBuf.Slice(0, 2)); // extra length
        stream.Write(encodedFilename, 0, encodedFilename.Length);

        if (extralength != 0)
        {
            stream.Write(new byte[extralength], 0, extralength); // reserve space for zip64 data
            entry.Zip64HeaderOffset = (ushort)(6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length);
        }

        return 6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length + extralength;
    }

    private void WriteFooter(uint crc, uint compressed, uint uncompressed) =>
        WriteFooter(OutputStream.NotNull(), crc, compressed, uncompressed);

    private static void WriteFooter(Stream stream, uint crc, uint compressed, uint uncompressed)
    {
        Span<byte> intBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, crc);
        stream.Write(intBuf);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, compressed);
        stream.Write(intBuf);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, uncompressed);
        stream.Write(intBuf);
    }

    private void WriteEndRecord(ulong size) => WriteEndRecord(OutputStream.NotNull(), size);

    private void WriteEndRecord(Stream stream, ulong size)
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
            stream.Write(stackalloc byte[] { 80, 75, 6, 6 });

            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)recordlen);
            stream.Write(intBuf); // Size of zip64 end of central directory record
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 45);
            stream.Write(intBuf.Slice(0, 2)); // Made by
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 45);
            stream.Write(intBuf.Slice(0, 2)); // Version needed

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            stream.Write(intBuf.Slice(0, 4)); // Disk number
            stream.Write(intBuf.Slice(0, 4)); // Central dir disk

            // TODO: entries.Count is int, so max 2^31 files
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)entries.Count);
            stream.Write(intBuf); // Entries in this disk
            stream.Write(intBuf); // Total entries
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, size);
            stream.Write(intBuf); // Central Directory size
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)streamPosition);
            stream.Write(intBuf); // Disk offset

            // Write zip64 end of central directory locator
            stream.Write(stackalloc byte[] { 80, 75, 6, 7 });

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            stream.Write(intBuf.Slice(0, 4)); // Entry disk
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)streamPosition + size);
            stream.Write(intBuf); // Offset to the zip64 central directory
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 1);
            stream.Write(intBuf.Slice(0, 4)); // Number of disks

            streamPosition += 4 + 8 + recordlen + (4 + 4 + 8 + 4);
        }

        // Write normal end of central directory record
        stream.Write(stackalloc byte[] { 80, 75, 5, 6, 0, 0, 0, 0 });
        BinaryPrimitives.WriteUInt16LittleEndian(
            intBuf,
            (ushort)(entries.Count < 0xFFFF ? entries.Count : 0xFFFF)
        );
        stream.Write(intBuf.Slice(0, 2));
        stream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, sizevalue);
        stream.Write(intBuf.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, streampositionvalue);
        stream.Write(intBuf.Slice(0, 4));
        var encodedComment = WriterOptions.ArchiveEncoding.Encode(zipComment);
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
        stream.Write(intBuf.Slice(0, 2));
        stream.Write(encodedComment, 0, encodedComment.Length);
    }
}
