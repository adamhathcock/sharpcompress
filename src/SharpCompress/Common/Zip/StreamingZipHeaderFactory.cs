using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal partial class StreamingZipHeaderFactory : ZipHeaderFactory
{
    private IEnumerable<ZipEntry>? _entries;

    internal StreamingZipHeaderFactory(
        string? password,
        IArchiveEncoding archiveEncoding,
        IEnumerable<ZipEntry>? entries
    )
        : base(StreamingMode.Streaming, password, archiveEncoding) => _entries = entries;

    internal IEnumerable<ZipHeader> ReadStreamHeader(Stream stream)
    {
        // Use Create to avoid double-wrapping if stream is already a SharpCompressStream,
        // and to preserve seekability for DataDescriptorStream which needs to seek backward
        var sharpCompressStream = SharpCompressStream.Create(stream);
        var reader = new BinaryReader(
            sharpCompressStream,
            System.Text.Encoding.Default,
            leaveOpen: true
        );

        try
        {
            while (true)
            {
                uint headerBytes = 0;
                if (
                    _lastEntryHeader != null
                    && FlagUtility.HasFlag(
                        _lastEntryHeader.Flags,
                        HeaderFlags.UsePostDataDescriptor
                    )
                )
                {
                    if (_lastEntryHeader.Part is null)
                    {
                        continue;
                    }

                    // removed requirement for FixStreamedFileLocation()

                    var pos = sharpCompressStream.CanSeek
                        ? (long?)sharpCompressStream.Position
                        : null;

                    var crc = reader.ReadUInt32();
                    if (crc == POST_DATA_DESCRIPTOR)
                    {
                        crc = reader.ReadUInt32();
                    }
                    _lastEntryHeader.Crc = crc;
                    _lastEntryHeader.IsCrcAvailable = true;

                    //attempt 32bit read
                    ulong compSize = reader.ReadUInt32();
                    ulong uncompSize = reader.ReadUInt32();
                    headerBytes = reader.ReadUInt32();

                    //check for zip64 sentinel or unexpected header
                    bool isSentinel = compSize == 0xFFFFFFFF || uncompSize == 0xFFFFFFFF;
                    bool isHeader = headerBytes == 0x04034b50 || headerBytes == 0x02014b50;

                    if (!isHeader && !isSentinel)
                    {
                        //reshuffle into 64-bit values
                        compSize = (uncompSize << 32) | compSize;
                        // headerBytes holds the low word; the high word follows on the wire.
                        uncompSize = headerBytes | ((ulong)reader.ReadUInt32() << 32);
                        headerBytes = reader.ReadUInt32();
                    }
                    else if (isSentinel)
                    {
                        //standards-compliant zip64 descriptor
                        compSize = reader.ReadUInt64();
                        uncompSize = reader.ReadUInt64();
                    }

                    _lastEntryHeader.CompressedSize = (long)compSize;
                    _lastEntryHeader.UncompressedSize = (long)uncompSize;

                    if (pos.HasValue)
                    {
                        _lastEntryHeader.DataStartPosition = pos - _lastEntryHeader.CompressedSize;
                    }
                }
                else if (_lastEntryHeader != null && _lastEntryHeader.IsZip64)
                {
                    if (_lastEntryHeader.Part is null)
                    {
                        continue;
                    }

                    //reader = ((StreamingZipFilePart)_lastEntryHeader.Part).FixStreamedFileLocation(
                    //    ref sharpCompressStream
                    //);

                    var pos = sharpCompressStream.CanSeek
                        ? (long?)sharpCompressStream.Position
                        : null;

                    headerBytes = reader.ReadUInt32();

                    // A Zip64 entry that does not use a post-data descriptor stores its real CRC
                    // and sizes in the local header's Zip64 extra field, so a header signature
                    // (the next local entry, or the central directory) follows the data directly.
                    // In that case the entry's metadata is already correct and must not be
                    // overwritten with bytes read from the following header. We have only consumed
                    // the 4-byte signature, so fall through and parse this header normally.
                    if (headerBytes == 0x04034b50 || headerBytes == 0x02014b50)
                    {
                        if (pos.HasValue)
                        {
                            _lastEntryHeader.DataStartPosition =
                                pos - _lastEntryHeader.CompressedSize;
                        }
                    }
                    else
                    {
                        // A data descriptor follows. Recover the CRC and sizes from it; the
                        // descriptor can carry either 32-bit or 64-bit sizes.
                        _ = reader.ReadUInt16(); // version
                        _ = reader.ReadUInt16(); // flags
                        _ = reader.ReadUInt16(); // compressionMethod
                        _ = reader.ReadUInt16(); // lastModifiedDate
                        _ = reader.ReadUInt16(); // lastModifiedTime

                        var crc = reader.ReadUInt32();

                        if (crc == POST_DATA_DESCRIPTOR)
                        {
                            crc = reader.ReadUInt32();
                        }
                        _lastEntryHeader.Crc = crc;
                        _lastEntryHeader.IsCrcAvailable = true;

                        // The DataDescriptor can be either 64bit or 32bit
                        var compressed_size = reader.ReadUInt32();
                        var uncompressed_size = reader.ReadUInt32();

                        var test_64bit = ((long)uncompressed_size << 32) | compressed_size;
                        if (test_64bit == _lastEntryHeader.CompressedSize)
                        {
                            _lastEntryHeader.UncompressedSize =
                                ((long)reader.ReadUInt32() << 32) | headerBytes;
                            headerBytes = reader.ReadUInt32();
                        }
                        else
                        {
                            _lastEntryHeader.UncompressedSize = uncompressed_size;
                        }

                        if (pos.HasValue)
                        {
                            _lastEntryHeader.DataStartPosition =
                                pos - _lastEntryHeader.CompressedSize;

                            // 4 = First 4 bytes of the entry header (i.e. 50 4B 03 04)
                            sharpCompressStream.Position = pos.Value + 4;
                        }
                    }
                }
                else
                {
                    try
                    {
                        headerBytes = reader.ReadUInt32();
                    }
                    catch (EndOfStreamException ex)
                    {
                        throw new InvalidFormatException(
                            "Unexpected end of stream while reading ZIP archive",
                            ex
                        );
                    }
                }

                _lastEntryHeader = null;
                var header = ReadHeader(headerBytes, reader);
                if (header is null)
                {
                    yield break;
                }

                //entry could be zero bytes so we need to know that.
                if (header.ZipHeaderType == ZipHeaderType.LocalEntry)
                {
                    var local_header = ((LocalEntryHeader)header);
                    var dir_header = _entries?.FirstOrDefault(entry =>
                        entry.Key == local_header.Name
                        && local_header.CompressedSize == 0
                        && local_header.UncompressedSize == 0
                        && local_header.Crc == 0
                        && local_header.IsDirectory == false
                    );

                    if (dir_header != null)
                    {
                        local_header.UncompressedSize = dir_header.Size;
                        local_header.CompressedSize = dir_header.CompressedSize;
                        local_header.Crc = (uint)dir_header.Crc;
                        local_header.IsCrcAvailable = true;
                    }

                    // If we have CompressedSize, there is data to be read
                    if (local_header.CompressedSize > 0)
                    {
                        header.HasData = true;
                    } // Check if zip is streaming ( Length is 0 and is declared in PostDataDescriptor )
                    else if (local_header.Flags.HasFlag(HeaderFlags.UsePostDataDescriptor))
                    {
                        // Peek ahead to check if next data is a header or file data.
                        // Use the IStreamStack.Rewind mechanism to give back the peeked bytes.
                        var nextHeaderBytes = reader.ReadUInt32();
                        sharpCompressStream.Rewind(sizeof(uint));

                        // Check if next data is PostDataDescriptor, streamed file with 0 length
                        header.HasData = !IsHeader(nextHeaderBytes);
                    }
                    else // We are not streaming and compressed size is 0, we have no data
                    {
                        header.HasData = false;
                    }
                }
                yield return header;
            }
        }
        finally
        {
            reader.Dispose();
        }
    }
}
