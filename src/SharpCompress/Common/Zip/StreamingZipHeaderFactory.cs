using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip;

internal class StreamingZipHeaderFactory : ZipHeaderFactory
{
    private IEnumerable<ZipEntry>? _entries;

    internal StreamingZipHeaderFactory(
        string? password,
        ArchiveEncoding archiveEncoding,
        IEnumerable<ZipEntry>? entries
    )
        : base(StreamingMode.Streaming, password, archiveEncoding) => _entries = entries;

    internal IEnumerable<ZipHeader> ReadStreamHeader(Stream stream)
    {
        RewindableStream rewindableStream;

        if (stream is RewindableStream rs)
        {
            rewindableStream = rs;
        }
        else
        {
            rewindableStream = new RewindableStream(stream);
        }
        while (true)
        {
            ZipHeader? header;
            var reader = new BinaryReader(rewindableStream);
            uint headerBytes = 0;
            if (
                _lastEntryHeader != null
                && FlagUtility.HasFlag(_lastEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor)
            )
            {
                if (_lastEntryHeader.Part is null)
                {
                    continue;
                }
                reader = ((StreamingZipFilePart)_lastEntryHeader.Part).FixStreamedFileLocation(
                    ref rewindableStream
                );

                var pos = rewindableStream.CanSeek ? (long?)rewindableStream.Position : null;

                var crc = reader.ReadUInt32();
                if (crc == POST_DATA_DESCRIPTOR)
                {
                    crc = reader.ReadUInt32();
                }
                _lastEntryHeader.Crc = crc;

                // The DataDescriptor can be either 64bit or 32bit
                var compressed_size = reader.ReadUInt32();
                var uncompressed_size = reader.ReadUInt32();

                // Check if we have header or 64bit DataDescriptor
                headerBytes = reader.ReadUInt32();
                var test_header = !(headerBytes == 0x04034b50 || headerBytes == 0x02014b50);

                var test_64bit = ((long)uncompressed_size << 32) | compressed_size;
                if (test_64bit == _lastEntryHeader.CompressedSize && test_header)
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
                    _lastEntryHeader.DataStartPosition = pos - _lastEntryHeader.CompressedSize;
                }
            }
            else if (_lastEntryHeader != null && _lastEntryHeader.IsZip64)
            {
                if (_lastEntryHeader.Part is null)
                    continue;

                reader = ((StreamingZipFilePart)_lastEntryHeader.Part).FixStreamedFileLocation(
                    ref rewindableStream
                );

                var pos = rewindableStream.CanSeek ? (long?)rewindableStream.Position : null;

                headerBytes = reader.ReadUInt32();

                var version = reader.ReadUInt16();
                var flags = (HeaderFlags)reader.ReadUInt16();
                var compressionMethod = (ZipCompressionMethod)reader.ReadUInt16();
                var lastModifiedDate = reader.ReadUInt16();
                var lastModifiedTime = reader.ReadUInt16();

                var crc = reader.ReadUInt32();

                if (crc == POST_DATA_DESCRIPTOR)
                {
                    crc = reader.ReadUInt32();
                }
                _lastEntryHeader.Crc = crc;

                // The DataDescriptor can be either 64bit or 32bit
                var compressed_size = reader.ReadUInt32();
                var uncompressed_size = reader.ReadUInt32();

                // Check if we have header or 64bit DataDescriptor
                var test_header = !(headerBytes == 0x04034b50 || headerBytes == 0x02014b50);

                var test_64bit = ((long)uncompressed_size << 32) | compressed_size;
                if (test_64bit == _lastEntryHeader.CompressedSize && test_header)
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
                    _lastEntryHeader.DataStartPosition = pos - _lastEntryHeader.CompressedSize;

                    // 4 = First 4 bytes of the entry header (i.e. 50 4B 03 04)
                    rewindableStream.Position = pos.Value + 4;
                }
            }
            else
            {
                headerBytes = reader.ReadUInt32();
            }

            _lastEntryHeader = null;
            header = ReadHeader(headerBytes, reader);
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
                }

                // If we have CompressedSize, there is data to be read
                if (local_header.CompressedSize > 0)
                {
                    header.HasData = true;
                } // Check if zip is streaming ( Length is 0 and is declared in PostDataDescriptor )
                else if (local_header.Flags.HasFlag(HeaderFlags.UsePostDataDescriptor))
                {
                    var isRecording = rewindableStream.IsRecording;
                    if (!isRecording)
                    {
                        rewindableStream.StartRecording();
                    }
                    var nextHeaderBytes = reader.ReadUInt32();

                    // Check if next data is PostDataDescriptor, streamed file with 0 length
                    header.HasData = !IsHeader(nextHeaderBytes);
                    rewindableStream.Rewind(!isRecording);
                }
                else // We are not streaming and compressed size is 0, we have no data
                {
                    header.HasData = false;
                }
            }
            yield return header;
        }
    }
}
