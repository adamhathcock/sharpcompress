using System;
using System.Buffers;
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

#if NETFRAMEWORK || NETSTANDARD2_0
public class ZipWriter : AbstractWriter
{
    private readonly CompressionType _compressionType;
    private readonly CompressionLevel _compressionLevel;
    private readonly List<ZipCentralDirectoryEntry> _entries = new();
    private readonly string _zipComment;
    private long _streamPosition;
    private PpmdProperties? _ppmdProps;
    private readonly bool _isZip64;

    public ZipWriter(Stream destination, ZipWriterOptions zipWriterOptions)
        : base(ArchiveType.Zip, zipWriterOptions)
    {
        _zipComment = zipWriterOptions.ArchiveComment ?? string.Empty;
        _isZip64 = zipWriterOptions.UseZip64;
        if (destination.CanSeek)
        {
            _streamPosition = destination.Position;
        }

        _compressionType = zipWriterOptions.CompressionType;
        _compressionLevel = zipWriterOptions.DeflateCompressionLevel;

        if (WriterOptions.LeaveStreamOpen)
        {
            destination = NonDisposingStream.Create(destination);
        }
        InitalizeStream(destination);
    }

    private PpmdProperties PpmdProperties => _ppmdProps ??= new PpmdProperties();

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            ulong size = 0;
            foreach (var entry in _entries)
            {
                size += entry.Write(OutputStream);
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

    public override void Write(string entryPath, Stream source, DateTime? modificationTime) =>
        Write(
            entryPath,
            source,
            new ZipWriterEntryOptions() { ModificationDateTime = modificationTime }
        );

    public void Write(string entryPath, Stream source, ZipWriterEntryOptions zipWriterEntryOptions)
    {
        using var output = WriteToStream(entryPath, zipWriterEntryOptions);
        source.TransferTo(output);
    }

    public Stream WriteToStream(string entryPath, ZipWriterEntryOptions options)
    {
        var compression = ToZipCompressionMethod(options.CompressionType ?? _compressionType);

        entryPath = NormalizeFilename(entryPath);
        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;
        var entry = new ZipCentralDirectoryEntry(
            compression,
            entryPath,
            (ulong)_streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime
        };

        // Use the archive default setting for zip64 and allow overrides
        var useZip64 = _isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        var headersize = (uint)WriteHeader(entryPath, options, entry, useZip64);
        _streamPosition += headersize;
        return new ZipWritingStream(
            this,
            OutputStream,
            entry,
            compression,
            options.DeflateCompressionLevel ?? _compressionLevel
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

    private int WriteHeader(
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64
    )
    {
        // We err on the side of caution until the zip specification clarifies how to support this
        if (!OutputStream.CanSeek && useZip64)
        {
            throw new NotSupportedException(
                "Zip64 extensions are not supported on non-seekable streams"
            );
        }

        var explicitZipCompressionInfo = ToZipCompressionMethod(
            zipWriterEntryOptions.CompressionType ?? _compressionType
        );
        var encodedFilename = WriterOptions.ArchiveEncoding.Encode(filename);

        Span<byte> intBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ZipHeaderFactory.ENTRY_HEADER_BYTES);
        OutputStream.Write(intBuf);
        if (explicitZipCompressionInfo == ZipCompressionMethod.Deflate)
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
            : 0;
        if (!OutputStream.CanSeek)
        {
            flags |= HeaderFlags.UsePostDataDescriptor;

            if (explicitZipCompressionInfo == ZipCompressionMethod.LZMA)
            {
                flags |= HeaderFlags.Bit1; // eos marker
            }
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
        OutputStream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)explicitZipCompressionInfo);
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

        var extralength = 0;
        if (OutputStream.CanSeek && useZip64)
        {
            extralength = 2 + 2 + 8 + 8;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
        OutputStream.Write(intBuf.Slice(0, 2)); // extra length
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
            _entries.Count > ushort.MaxValue
            || _streamPosition >= uint.MaxValue
            || size >= uint.MaxValue;

        var sizevalue = size >= uint.MaxValue ? uint.MaxValue : (uint)size;
        var streampositionvalue =
            _streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)_streamPosition;

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
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_entries.Count);
            OutputStream.Write(intBuf); // Entries in this disk
            OutputStream.Write(intBuf); // Total entries
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, size);
            OutputStream.Write(intBuf); // Central Directory size
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_streamPosition);
            OutputStream.Write(intBuf); // Disk offset

            // Write zip64 end of central directory locator
            OutputStream.Write(stackalloc byte[] { 80, 75, 6, 7 });

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            OutputStream.Write(intBuf.Slice(0, 4)); // Entry disk
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_streamPosition + size);
            OutputStream.Write(intBuf); // Offset to the zip64 central directory
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 1);
            OutputStream.Write(intBuf.Slice(0, 4)); // Number of disks

            _streamPosition += 4 + 8 + recordlen + (4 + 4 + 8 + 4);
        }

        // Write normal end of central directory record
        OutputStream.Write(stackalloc byte[] { 80, 75, 5, 6, 0, 0, 0, 0 });
        BinaryPrimitives.WriteUInt16LittleEndian(
            intBuf,
            (ushort)(_entries.Count < 0xFFFF ? _entries.Count : 0xFFFF)
        );
        OutputStream.Write(intBuf.Slice(0, 2));
        OutputStream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, sizevalue);
        OutputStream.Write(intBuf.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, streampositionvalue);
        OutputStream.Write(intBuf.Slice(0, 4));
        var encodedComment = WriterOptions.ArchiveEncoding.Encode(_zipComment);
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
        OutputStream.Write(intBuf.Slice(0, 2));
        OutputStream.Write(encodedComment, 0, encodedComment.Length);
    }

    #region Nested type: ZipWritingStream

    internal class ZipWritingStream : Stream
    {
        private readonly CRC32 _crc = new();
        private readonly ZipCentralDirectoryEntry _entry;
        private readonly Stream _originalStream;
        private readonly Stream _writeStream;
        private readonly ZipWriter _writer;
        private readonly ZipCompressionMethod _zipCompressionMethod;
        private readonly CompressionLevel _compressionLevel;
        private CountingWritableSubStream? _counting;
        private ulong _decompressed;

        // Flag to prevent throwing exceptions on Dispose
        private bool _limitsExceeded;
        private bool _isDisposed;

        internal ZipWritingStream(
            ZipWriter writer,
            Stream originalStream,
            ZipCentralDirectoryEntry entry,
            ZipCompressionMethod zipCompressionMethod,
            CompressionLevel compressionLevel
        )
        {
            _writer = writer;
            _originalStream = originalStream;
            _writer = writer;
            _entry = entry;
            _zipCompressionMethod = zipCompressionMethod;
            _compressionLevel = compressionLevel;
            _writeStream = GetWriteStream(originalStream);
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
            _counting = new CountingWritableSubStream(writeStream);
            Stream output = _counting;
            switch (_zipCompressionMethod)
            {
                case ZipCompressionMethod.None:
                {
                    return output;
                }
                case ZipCompressionMethod.Deflate:
                {
                    return new DeflateStream(
                        _counting,
                        CompressionMode.Compress,
                        _compressionLevel
                    );
                }
                case ZipCompressionMethod.BZip2:
                {
                    return new BZip2Stream(_counting, CompressionMode.Compress, false);
                }
                case ZipCompressionMethod.LZMA:
                {
                    _counting.WriteByte(9);
                    _counting.WriteByte(20);
                    _counting.WriteByte(5);
                    _counting.WriteByte(0);

                    var lzmaStream = new LzmaStream(
                        new LzmaEncoderProperties(!_originalStream.CanSeek),
                        false,
                        _counting
                    );
                    _counting.Write(lzmaStream.Properties, 0, lzmaStream.Properties.Length);
                    return lzmaStream;
                }
                case ZipCompressionMethod.PPMd:
                {
                    _counting.Write(_writer.PpmdProperties.Properties, 0, 2);
                    return new PpmdStream(_writer.PpmdProperties, _counting, true);
                }
                default:
                {
                    throw new NotSupportedException("CompressionMethod: " + _zipCompressionMethod);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            base.Dispose(disposing);
            if (disposing)
            {
                _writeStream.Dispose();

                if (_limitsExceeded)
                {
                    // We have written invalid data into the archive,
                    // so we destroy it now, instead of allowing the user to continue
                    // with a defunct archive
                    _originalStream.Dispose();
                    return;
                }

                _entry.Crc = (uint)_crc.Crc32Result;
                _entry.Compressed = _counting!.Count;
                _entry.Decompressed = _decompressed;

                var zip64 =
                    _entry.Compressed >= uint.MaxValue || _entry.Decompressed >= uint.MaxValue;
                var compressedvalue = zip64 ? uint.MaxValue : (uint)_counting.Count;
                var decompressedvalue = zip64 ? uint.MaxValue : (uint)_entry.Decompressed;

                if (_originalStream.CanSeek)
                {
                    _originalStream.Position = (long)(_entry.HeaderOffset + 6);
                    _originalStream.WriteByte(0);

                    if (_counting.Count == 0 && _entry.Decompressed == 0)
                    {
                        // set compression to STORED for zero byte files (no compression data)
                        _originalStream.Position = (long)(_entry.HeaderOffset + 8);
                        _originalStream.WriteByte(0);
                        _originalStream.WriteByte(0);
                    }

                    _originalStream.Position = (long)(_entry.HeaderOffset + 14);

                    _writer.WriteFooter(_entry.Crc, compressedvalue, decompressedvalue);

                    // Ideally, we should not throw from Dispose()
                    // We should not get here as the Write call checks the limits
                    if (zip64 && _entry.Zip64HeaderOffset == 0)
                    {
                        throw new NotSupportedException(
                            "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                        );
                    }

                    // If we have pre-allocated space for zip64 data,
                    // fill it out, even if it is not required
                    if (_entry.Zip64HeaderOffset != 0)
                    {
                        _originalStream.Position = (long)(
                            _entry.HeaderOffset + _entry.Zip64HeaderOffset
                        );
                        Span<byte> intBuf = stackalloc byte[8];
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                        _originalStream.Write(intBuf.Slice(0, 2));
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 8 + 8);
                        _originalStream.Write(intBuf.Slice(0, 2));

                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, _entry.Decompressed);
                        _originalStream.Write(intBuf);
                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, _entry.Compressed);
                        _originalStream.Write(intBuf);
                    }

                    _originalStream.Position = _writer._streamPosition + (long)_entry.Compressed;
                    _writer._streamPosition += (long)_entry.Compressed;
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
                    _originalStream.Write(intBuf);
                    _writer.WriteFooter(_entry.Crc, compressedvalue, decompressedvalue);
                    _writer._streamPosition += (long)_entry.Compressed + 16;
                }
                _writer._entries.Add(_entry);
            }
        }

        public override void Flush() => _writeStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            // We check the limits first, because we can keep the archive consistent
            // if we can prevent the writes from happening
            if (_entry.Zip64HeaderOffset == 0)
            {
                // Pre-check, the counting.Count is not exact, as we do not know the size before having actually compressed it
                if (
                    _limitsExceeded
                    || ((_decompressed + (uint)count) > uint.MaxValue)
                    || (_counting!.Count + (uint)count) > uint.MaxValue
                )
                {
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }
            }

            _decompressed += (uint)count;
            _crc.SlurpBlock(buffer, offset, count);
            _writeStream.Write(buffer, offset, count);

            if (_entry.Zip64HeaderOffset == 0)
            {
                // Post-check, this is accurate
                if ((_decompressed > uint.MaxValue) || _counting!.Count > uint.MaxValue)
                {
                    // We have written the data, so the archive is now broken
                    // Throwing the exception here, allows us to avoid
                    // throwing an exception in Dispose() which is discouraged
                    // as it can mask other errors
                    _limitsExceeded = true;
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }
            }
        }
    }

    #endregion Nested type: ZipWritingStream
}
#else

public class ZipWriter : AbstractWriter
{
    private static readonly byte[] ZIP64_END_OF_DIRECTORY = [80, 75, 6, 6];
    private static readonly byte[] END_OF_DIRECTORY = [80, 75, 6, 7];
    private readonly CompressionType _compressionType;
    private readonly CompressionLevel _compressionLevel;
    private readonly List<ZipCentralDirectoryEntry> _entries = new();
    private readonly string _zipComment;
    private long _streamPosition;
    private PpmdProperties? _ppmdProps;
    private readonly bool _isZip64;
    private bool _isDisposed;

    public ZipWriter(Stream destination, ZipWriterOptions zipWriterOptions)
        : base(ArchiveType.Zip, zipWriterOptions)
    {
        _zipComment = zipWriterOptions.ArchiveComment ?? string.Empty;
        _isZip64 = zipWriterOptions.UseZip64;
        if (destination.CanSeek)
        {
            _streamPosition = destination.Position;
        }

        _compressionType = zipWriterOptions.CompressionType;
        _compressionLevel = zipWriterOptions.DeflateCompressionLevel;

        if (WriterOptions.LeaveStreamOpen)
        {
            destination = NonDisposingStream.Create(destination);
        }
        InitalizeStream(destination);
    }

    private PpmdProperties PpmdProperties => _ppmdProps ??= new PpmdProperties();

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        ulong size = 0;
        foreach (var entry in _entries)
        {
            size += entry.Write(OutputStream);
        }
        await WriteEndRecordAsync(size, CancellationToken.None).ConfigureAwait(false);
        _isDisposed = true;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            ulong size = 0;
            foreach (var entry in _entries)
            {
                size += entry.Write(OutputStream);
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

    public override void Write(string entryPath, Stream source, DateTime? modificationTime) =>
        Write(
            entryPath,
            source,
            new ZipWriterEntryOptions() { ModificationDateTime = modificationTime }
        );

    public void Write(string entryPath, Stream source, ZipWriterEntryOptions zipWriterEntryOptions)
    {
        using var output = WriteToStream(entryPath, zipWriterEntryOptions);
        source.TransferTo(output);
    }

    public override async ValueTask WriteAsync(
        string entryPath,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken
    ) =>
        await WriteAsync(
            entryPath,
            source,
            new ZipWriterEntryOptions() { ModificationDateTime = modificationTime },
            cancellationToken
        );

    public async ValueTask WriteAsync(
        string entryPath,
        Stream source,
        ZipWriterEntryOptions zipWriterEntryOptions,
        CancellationToken cancellationToken
    )
    {
        await using var output = await WriteToStreamAsync(
            entryPath,
            zipWriterEntryOptions,
            cancellationToken
        );
        await source.CopyToAsync(output, cancellationToken);
    }

    public Stream WriteToStream(string entryPath, ZipWriterEntryOptions options)
    {
        var compression = ToZipCompressionMethod(options.CompressionType ?? _compressionType);

        entryPath = NormalizeFilename(entryPath);
        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;
        var entry = new ZipCentralDirectoryEntry(
            compression,
            entryPath,
            (ulong)_streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime
        };

        // Use the archive default setting for zip64 and allow overrides
        var useZip64 = _isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        var headersize = (uint)WriteHeader(entryPath, options, entry, useZip64);
        _streamPosition += headersize;
        return new ZipWritingStream(
            this,
            OutputStream,
            entry,
            compression,
            options.DeflateCompressionLevel ?? _compressionLevel
        );
    }

    public async ValueTask<Stream> WriteToStreamAsync(
        string entryPath,
        ZipWriterEntryOptions options,
        CancellationToken cancellationToken
    )
    {
        var compression = ToZipCompressionMethod(options.CompressionType ?? _compressionType);

        entryPath = NormalizeFilename(entryPath);
        options.ModificationDateTime ??= DateTime.Now;
        options.EntryComment ??= string.Empty;
        var entry = new ZipCentralDirectoryEntry(
            compression,
            entryPath,
            (ulong)_streamPosition,
            WriterOptions.ArchiveEncoding
        )
        {
            Comment = options.EntryComment,
            ModificationTime = options.ModificationDateTime
        };

        // Use the archive default setting for zip64 and allow overrides
        var useZip64 = _isZip64;
        if (options.EnableZip64.HasValue)
        {
            useZip64 = options.EnableZip64.Value;
        }

        var headersize = (uint)
            await WriteHeaderAsync(entryPath, options, entry, useZip64, cancellationToken)
                .ConfigureAwait(false);
        _streamPosition += headersize;
        return new ZipWritingStream(
            this,
            OutputStream,
            entry,
            compression,
            options.DeflateCompressionLevel ?? _compressionLevel
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

    private int WriteHeader(
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64
    )
    {
        // We err on the side of caution until the zip specification clarifies how to support this
        if (!OutputStream.CanSeek && useZip64)
        {
            throw new NotSupportedException(
                "Zip64 extensions are not supported on non-seekable streams"
            );
        }

        var explicitZipCompressionInfo = ToZipCompressionMethod(
            zipWriterEntryOptions.CompressionType ?? _compressionType
        );
        var encodedFilename = WriterOptions.ArchiveEncoding.Encode(filename);

        Span<byte> intBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ZipHeaderFactory.ENTRY_HEADER_BYTES);
        OutputStream.Write(intBuf);
        if (explicitZipCompressionInfo == ZipCompressionMethod.Deflate)
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
            : 0;
        if (!OutputStream.CanSeek)
        {
            flags |= HeaderFlags.UsePostDataDescriptor;

            if (explicitZipCompressionInfo == ZipCompressionMethod.LZMA)
            {
                flags |= HeaderFlags.Bit1; // eos marker
            }
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
        OutputStream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)explicitZipCompressionInfo);
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

        var extralength = 0;
        if (OutputStream.CanSeek && useZip64)
        {
            extralength = 2 + 2 + 8 + 8;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
        OutputStream.Write(intBuf.Slice(0, 2)); // extra length
        OutputStream.Write(encodedFilename, 0, encodedFilename.Length);

        if (extralength != 0)
        {
            OutputStream.Write(new byte[extralength], 0, extralength); // reserve space for zip64 data
            entry.Zip64HeaderOffset = (ushort)(6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length);
        }

        return 6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length + extralength;
    }

    private async ValueTask<int> WriteHeaderAsync(
        string filename,
        ZipWriterEntryOptions zipWriterEntryOptions,
        ZipCentralDirectoryEntry entry,
        bool useZip64,
        CancellationToken cancellationToken
    )
    {
        // We err on the side of caution until the zip specification clarifies how to support this
        if (!OutputStream.CanSeek && useZip64)
        {
            throw new NotSupportedException(
                "Zip64 extensions are not supported on non-seekable streams"
            );
        }

        var explicitZipCompressionInfo = ToZipCompressionMethod(
            zipWriterEntryOptions.CompressionType ?? _compressionType
        );
        var encodedFilename = WriterOptions.ArchiveEncoding.Encode(filename);

        var intBuf = ArrayPool<byte>.Shared.Rent(4);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, ZipHeaderFactory.ENTRY_HEADER_BYTES);
        await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false);
        if (explicitZipCompressionInfo == ZipCompressionMethod.Deflate)
        {
            if (OutputStream.CanSeek && useZip64)
            {
                await OutputStream
                    .WriteAsync([45, 0], 0, 2, cancellationToken)
                    .ConfigureAwait(false); //smallest allowed version for zip64
            }
            else
            {
                await OutputStream
                    .WriteAsync([20, 0], 0, 2, cancellationToken)
                    .ConfigureAwait(false); //older version which is more compatible
            }
        }
        else
        {
            await OutputStream.WriteAsync([63, 0], 0, 2, cancellationToken).ConfigureAwait(false); //version says we used PPMd or LZMA
        }
        var flags = Equals(WriterOptions.ArchiveEncoding.GetEncoding(), Encoding.UTF8)
            ? HeaderFlags.Efs
            : 0;
        if (!OutputStream.CanSeek)
        {
            flags |= HeaderFlags.UsePostDataDescriptor;

            if (explicitZipCompressionInfo == ZipCompressionMethod.LZMA)
            {
                flags |= HeaderFlags.Bit1; // eos marker
            }
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)flags);
        await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false);
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)explicitZipCompressionInfo);

        await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false); // zipping method
        BinaryPrimitives.WriteUInt32LittleEndian(
            intBuf,
            zipWriterEntryOptions.ModificationDateTime.DateTimeToDosTime()
        );
        await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false);

        // zipping date and time
        await OutputStream
            .WriteAsync([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], cancellationToken)
            .ConfigureAwait(false);

        // unused CRC, un/compressed size, updated later
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedFilename.Length);

        await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false); // filename length

        var extralength = 0;
        if (OutputStream.CanSeek && useZip64)
        {
            extralength = 2 + 2 + 8 + 8;
        }

        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)extralength);
        await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false); // extra length
        await OutputStream.WriteAsync(encodedFilename, cancellationToken).ConfigureAwait(false);

        if (extralength != 0)
        {
            await OutputStream
                .WriteAsync(new byte[extralength], cancellationToken)
                .ConfigureAwait(false); // reserve space for zip64 data
            entry.Zip64HeaderOffset = (ushort)(6 + 2 + 2 + 4 + 12 + 2 + 2 + encodedFilename.Length);
        }

        ArrayPool<byte>.Shared.Return(intBuf);
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

    private async ValueTask WriteFooterAsync(
        uint crc,
        uint compressed,
        uint uncompressed,
        CancellationToken cancellationToken
    )
    {
        var intBuf = ArrayPool<byte>.Shared.Rent(4);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, crc);
        await OutputStream.WriteAsync(intBuf, cancellationToken).ConfigureAwait(false);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, compressed);
        await OutputStream.WriteAsync(intBuf, cancellationToken).ConfigureAwait(false);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, uncompressed);
        await OutputStream.WriteAsync(intBuf, cancellationToken).ConfigureAwait(false);
        ArrayPool<byte>.Shared.Return(intBuf);
    }

    private void WriteEndRecord(ulong size)
    {
        var zip64EndOfCentralDirectoryNeeded =
            _entries.Count > ushort.MaxValue
            || _streamPosition >= uint.MaxValue
            || size >= uint.MaxValue;

        var sizevalue = size >= uint.MaxValue ? uint.MaxValue : (uint)size;
        var streampositionvalue =
            _streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)_streamPosition;

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
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_entries.Count);
            OutputStream.Write(intBuf); // Entries in this disk
            OutputStream.Write(intBuf); // Total entries
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, size);
            OutputStream.Write(intBuf); // Central Directory size
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_streamPosition);
            OutputStream.Write(intBuf); // Disk offset

            // Write zip64 end of central directory locator
            OutputStream.Write(stackalloc byte[] { 80, 75, 6, 7 });

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            OutputStream.Write(intBuf.Slice(0, 4)); // Entry disk
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_streamPosition + size);
            OutputStream.Write(intBuf); // Offset to the zip64 central directory
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 1);
            OutputStream.Write(intBuf.Slice(0, 4)); // Number of disks

            _streamPosition += 4 + 8 + recordlen + (4 + 4 + 8 + 4);
        }

        // Write normal end of central directory record
        OutputStream.Write(stackalloc byte[] { 80, 75, 5, 6, 0, 0, 0, 0 });
        BinaryPrimitives.WriteUInt16LittleEndian(
            intBuf,
            (ushort)(_entries.Count < 0xFFFF ? _entries.Count : 0xFFFF)
        );
        OutputStream.Write(intBuf.Slice(0, 2));
        OutputStream.Write(intBuf.Slice(0, 2));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, sizevalue);
        OutputStream.Write(intBuf.Slice(0, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, streampositionvalue);
        OutputStream.Write(intBuf.Slice(0, 4));
        var encodedComment = WriterOptions.ArchiveEncoding.Encode(_zipComment);
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
        OutputStream.Write(intBuf.Slice(0, 2));
        OutputStream.Write(encodedComment, 0, encodedComment.Length);
    }

    private async ValueTask WriteEndRecordAsync(ulong size, CancellationToken cancellationToken)
    {
        var zip64EndOfCentralDirectoryNeeded =
            _entries.Count > ushort.MaxValue
            || _streamPosition >= uint.MaxValue
            || size >= uint.MaxValue;

        var sizevalue = size >= uint.MaxValue ? uint.MaxValue : (uint)size;
        var streampositionvalue =
            _streamPosition >= uint.MaxValue ? uint.MaxValue : (uint)_streamPosition;

        var intBuf = ArrayPool<byte>.Shared.Rent(8);
        if (zip64EndOfCentralDirectoryNeeded)
        {
            var recordlen = 2 + 2 + 4 + 4 + 8 + 8 + 8 + 8;

            // Write zip64 end of central directory record
            await OutputStream
                .WriteAsync(ZIP64_END_OF_DIRECTORY, cancellationToken)
                .ConfigureAwait(false);

            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)recordlen);
            await OutputStream.WriteAsync(intBuf, 0, 8, cancellationToken).ConfigureAwait(false); // Size of zip64 end of central directory record
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 45);
            await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false); // Made by
            BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 45);

            await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false); // Version needed

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false); // Disk number
            await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false); // Central dir disk

            // TODO: entries.Count is int, so max 2^31 files
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_entries.Count);
            await OutputStream.WriteAsync(intBuf, 0, 8, cancellationToken).ConfigureAwait(false); // Entries in this disk
            await OutputStream.WriteAsync(intBuf, 0, 8, cancellationToken).ConfigureAwait(false); // Total entries
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, size);
            await OutputStream.WriteAsync(intBuf, 0, 8, cancellationToken).ConfigureAwait(false); // Central Directory size
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_streamPosition);
            await OutputStream.WriteAsync(intBuf, 0, 8, cancellationToken).ConfigureAwait(false); // Disk offset

            // Write zip64 end of central directory locator
            OutputStream.Write(stackalloc byte[] { 80, 75, 6, 7 });

            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 0);
            await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false); // Entry disk
            BinaryPrimitives.WriteUInt64LittleEndian(intBuf, (ulong)_streamPosition + size);
            await OutputStream.WriteAsync(intBuf, 0, 8, cancellationToken).ConfigureAwait(false); // Offset to the zip64 central directory
            BinaryPrimitives.WriteUInt32LittleEndian(intBuf, 1);
            await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false); // Number of disks

            _streamPosition += 4 + 8 + recordlen + (4 + 4 + 8 + 4);
        }

        // Write normal end of central directory record
        OutputStream.Write(END_OF_DIRECTORY);
        BinaryPrimitives.WriteUInt16LittleEndian(
            intBuf,
            (ushort)(_entries.Count < 0xFFFF ? _entries.Count : 0xFFFF)
        );
        await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false);
        await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, sizevalue);
        await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false);
        BinaryPrimitives.WriteUInt32LittleEndian(intBuf, streampositionvalue);
        await OutputStream.WriteAsync(intBuf, 0, 4, cancellationToken).ConfigureAwait(false);
        var encodedComment = WriterOptions.ArchiveEncoding.Encode(_zipComment);
        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, (ushort)encodedComment.Length);
        await OutputStream.WriteAsync(intBuf, 0, 2, cancellationToken).ConfigureAwait(false);
        await OutputStream
            .WriteAsync(encodedComment, 0, encodedComment.Length, cancellationToken)
            .ConfigureAwait(false);
        ArrayPool<byte>.Shared.Return(intBuf);
    }

#region Nested type: ZipWritingStream

    internal class ZipWritingStream : Stream
    {
        private readonly CRC32 _crc = new();
        private readonly ZipCentralDirectoryEntry _entry;
        private readonly Stream _originalStream;
        private readonly Stream _writeStream;
        private readonly ZipWriter _writer;
        private readonly ZipCompressionMethod _zipCompressionMethod;
        private readonly CompressionLevel _compressionLevel;
        private CountingWritableSubStream? _counting;
        private ulong _decompressed;

        // Flag to prevent throwing exceptions on Dispose
        private bool _limitsExceeded;
        private bool _isDisposed;

        internal ZipWritingStream(
            ZipWriter writer,
            Stream originalStream,
            ZipCentralDirectoryEntry entry,
            ZipCompressionMethod zipCompressionMethod,
            CompressionLevel compressionLevel
        )
        {
            _writer = writer;
            _originalStream = originalStream;
            _writer = writer;
            _entry = entry;
            _zipCompressionMethod = zipCompressionMethod;
            _compressionLevel = compressionLevel;
            _writeStream = GetWriteStream(originalStream);
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
            _counting = new CountingWritableSubStream(writeStream);
            Stream output = _counting;
            switch (_zipCompressionMethod)
            {
                case ZipCompressionMethod.None:
                {
                    return output;
                }
                case ZipCompressionMethod.Deflate:
                {
                    return new DeflateStream(
                        _counting,
                        CompressionMode.Compress,
                        _compressionLevel
                    );
                }
                case ZipCompressionMethod.BZip2:
                {
                    return new BZip2Stream(_counting, CompressionMode.Compress, false);
                }
                case ZipCompressionMethod.LZMA:
                {
                    _counting.WriteByte(9);
                    _counting.WriteByte(20);
                    _counting.WriteByte(5);
                    _counting.WriteByte(0);

                    var lzmaStream = new LzmaStream(
                        new LzmaEncoderProperties(!_originalStream.CanSeek),
                        false,
                        _counting
                    );
                    _counting.Write(lzmaStream.Properties, 0, lzmaStream.Properties.Length);
                    return lzmaStream;
                }
                case ZipCompressionMethod.PPMd:
                {
                    _counting.Write(_writer.PpmdProperties.Properties, 0, 2);
                    return new PpmdStream(_writer.PpmdProperties, _counting, true);
                }
                default:
                {
                    throw new NotSupportedException("CompressionMethod: " + _zipCompressionMethod);
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            await _writeStream.DisposeAsync();

            if (_limitsExceeded)
            {
                // We have written invalid data into the archive,
                // so we destroy it now, instead of allowing the user to continue
                // with a defunct archive
                await _originalStream.DisposeAsync();
                return;
            }

            _entry.Crc = (uint)_crc.Crc32Result;
            _entry.Compressed = _counting!.Count;
            _entry.Decompressed = _decompressed;

            var zip64 = _entry.Compressed >= uint.MaxValue || _entry.Decompressed >= uint.MaxValue;
            var compressedvalue = zip64 ? uint.MaxValue : (uint)_counting.Count;
            var decompressedvalue = zip64 ? uint.MaxValue : (uint)_entry.Decompressed;

            if (_originalStream.CanSeek)
            {
                _originalStream.Position = (long)(_entry.HeaderOffset + 6);
                _originalStream.WriteByte(0);

                if (_counting.Count == 0 && _entry.Decompressed == 0)
                {
                    // set compression to STORED for zero byte files (no compression data)
                    _originalStream.Position = (long)(_entry.HeaderOffset + 8);
                    _originalStream.WriteByte(0);
                    _originalStream.WriteByte(0);
                }

                _originalStream.Position = (long)(_entry.HeaderOffset + 14);

                await _writer.WriteFooterAsync(
                    _entry.Crc,
                    compressedvalue,
                    decompressedvalue,
                    CancellationToken.None
                );

                // Ideally, we should not throw from Dispose()
                // We should not get here as the Write call checks the limits
                if (zip64 && _entry.Zip64HeaderOffset == 0)
                {
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }

                // If we have pre-allocated space for zip64 data,
                // fill it out, even if it is not required
                if (_entry.Zip64HeaderOffset != 0)
                {
                    _originalStream.Position = (long)(
                        _entry.HeaderOffset + _entry.Zip64HeaderOffset
                    );
                    var intBuf = ArrayPool<byte>.Shared.Rent(8);
                    BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                    await _originalStream
                        .WriteAsync(intBuf, 0, 2, CancellationToken.None)
                        .ConfigureAwait(false);
                    BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 8 + 8);
                    await _originalStream
                        .WriteAsync(intBuf, 0, 2, CancellationToken.None)
                        .ConfigureAwait(false);

                    BinaryPrimitives.WriteUInt64LittleEndian(intBuf, _entry.Decompressed);
                    await _originalStream
                        .WriteAsync(intBuf, CancellationToken.None)
                        .ConfigureAwait(false);
                    BinaryPrimitives.WriteUInt64LittleEndian(intBuf, _entry.Compressed);
                    await _originalStream
                        .WriteAsync(intBuf, CancellationToken.None)
                        .ConfigureAwait(false);
                    ArrayPool<byte>.Shared.Return(intBuf);
                }

                _originalStream.Position = _writer._streamPosition + (long)_entry.Compressed;
                _writer._streamPosition += (long)_entry.Compressed;
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

                var intBuf = ArrayPool<byte>.Shared.Rent(4);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    intBuf,
                    ZipHeaderFactory.POST_DATA_DESCRIPTOR
                );
                await _originalStream
                    .WriteAsync(intBuf, CancellationToken.None)
                    .ConfigureAwait(false);
                await _writer
                    .WriteFooterAsync(
                        _entry.Crc,
                        compressedvalue,
                        decompressedvalue,
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);
                _writer._streamPosition += (long)_entry.Compressed + 16;
                ArrayPool<byte>.Shared.Return(intBuf);
            }
            _writer._entries.Add(_entry);
        }

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            base.Dispose(disposing);
            if (disposing)
            {
                _writeStream.Dispose();

                if (_limitsExceeded)
                {
                    // We have written invalid data into the archive,
                    // so we destroy it now, instead of allowing the user to continue
                    // with a defunct archive
                    _originalStream.Dispose();
                    return;
                }

                _entry.Crc = (uint)_crc.Crc32Result;
                _entry.Compressed = _counting!.Count;
                _entry.Decompressed = _decompressed;

                var zip64 =
                    _entry.Compressed >= uint.MaxValue || _entry.Decompressed >= uint.MaxValue;
                var compressedvalue = zip64 ? uint.MaxValue : (uint)_counting.Count;
                var decompressedvalue = zip64 ? uint.MaxValue : (uint)_entry.Decompressed;

                if (_originalStream.CanSeek)
                {
                    _originalStream.Position = (long)(_entry.HeaderOffset + 6);
                    _originalStream.WriteByte(0);

                    if (_counting.Count == 0 && _entry.Decompressed == 0)
                    {
                        // set compression to STORED for zero byte files (no compression data)
                        _originalStream.Position = (long)(_entry.HeaderOffset + 8);
                        _originalStream.WriteByte(0);
                        _originalStream.WriteByte(0);
                    }

                    _originalStream.Position = (long)(_entry.HeaderOffset + 14);

                    _writer.WriteFooter(_entry.Crc, compressedvalue, decompressedvalue);

                    // Ideally, we should not throw from Dispose()
                    // We should not get here as the Write call checks the limits
                    if (zip64 && _entry.Zip64HeaderOffset == 0)
                    {
                        throw new NotSupportedException(
                            "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                        );
                    }

                    // If we have pre-allocated space for zip64 data,
                    // fill it out, even if it is not required
                    if (_entry.Zip64HeaderOffset != 0)
                    {
                        _originalStream.Position = (long)(
                            _entry.HeaderOffset + _entry.Zip64HeaderOffset
                        );
                        Span<byte> intBuf = stackalloc byte[8];
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 0x0001);
                        _originalStream.Write(intBuf.Slice(0, 2));
                        BinaryPrimitives.WriteUInt16LittleEndian(intBuf, 8 + 8);
                        _originalStream.Write(intBuf.Slice(0, 2));

                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, _entry.Decompressed);
                        _originalStream.Write(intBuf);
                        BinaryPrimitives.WriteUInt64LittleEndian(intBuf, _entry.Compressed);
                        _originalStream.Write(intBuf);
                    }

                    _originalStream.Position = _writer._streamPosition + (long)_entry.Compressed;
                    _writer._streamPosition += (long)_entry.Compressed;
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
                    _originalStream.Write(intBuf);
                    _writer.WriteFooter(_entry.Crc, compressedvalue, decompressedvalue);
                    _writer._streamPosition += (long)_entry.Compressed + 16;
                }
                _writer._entries.Add(_entry);
            }
        }

        public override void Flush() => _writeStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            // We check the limits first, because we can keep the archive consistent
            // if we can prevent the writes from happening
            if (_entry.Zip64HeaderOffset == 0)
            {
                // Pre-check, the counting.Count is not exact, as we do not know the size before having actually compressed it
                if (
                    _limitsExceeded
                    || ((_decompressed + (uint)count) > uint.MaxValue)
                    || (_counting!.Count + (uint)count) > uint.MaxValue
                )
                {
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }
            }

            _decompressed += (uint)count;
            _crc.SlurpBlock(buffer, offset, count);
            _writeStream.Write(buffer, offset, count);

            if (_entry.Zip64HeaderOffset == 0)
            {
                // Post-check, this is accurate
                if ((_decompressed > uint.MaxValue) || _counting!.Count > uint.MaxValue)
                {
                    // We have written the data, so the archive is now broken
                    // Throwing the exception here, allows us to avoid
                    // throwing an exception in Dispose() which is discouraged
                    // as it can mask other errors
                    _limitsExceeded = true;
                    throw new NotSupportedException(
                        "Attempted to write a stream that is larger than 4GiB without setting the zip64 option"
                    );
                }
            }
        }
    }

#endregion Nested type: ZipWritingStream
}
#endif
