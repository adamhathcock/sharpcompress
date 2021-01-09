using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
    internal class StreamingZipHeaderFactory : ZipHeaderFactory
    {
        internal StreamingZipHeaderFactory(string? password, ArchiveEncoding archiveEncoding)
            : base(StreamingMode.Streaming, password, archiveEncoding)
        {
        }

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
                BinaryReader reader = new BinaryReader(rewindableStream);
                if (_lastEntryHeader != null &&
                    (FlagUtility.HasFlag(_lastEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor) || _lastEntryHeader.IsZip64))
                {
                    reader = ((StreamingZipFilePart)_lastEntryHeader.Part).FixStreamedFileLocation(ref rewindableStream);
                    long? pos = rewindableStream.CanSeek ? (long?)rewindableStream.Position : null;
                    uint crc = reader.ReadUInt32();
                    if (crc == POST_DATA_DESCRIPTOR)
                    {
                        crc = reader.ReadUInt32();
                    }
                    _lastEntryHeader.Crc = crc;
                    _lastEntryHeader.CompressedSize = reader.ReadUInt32();
                    _lastEntryHeader.UncompressedSize = reader.ReadUInt32();
                    if (pos.HasValue)
                    {
                        _lastEntryHeader.DataStartPosition = pos - _lastEntryHeader.CompressedSize;
                    }
                }
                _lastEntryHeader = null;
                uint headerBytes = reader.ReadUInt32();
                header = ReadHeader(headerBytes, reader);
                if (header is null)
                {
                    yield break;
                }

                //entry could be zero bytes so we need to know that.
                if (header.ZipHeaderType == ZipHeaderType.LocalEntry)
                {
                    var local_header = ((LocalEntryHeader)header);

                    // If we have CompressedSize, there is data to be read
                    if (local_header.CompressedSize > 0)
                    {
                        header.HasData = true;
                    } // Check if zip is streaming ( Length is 0 and is declared in PostDataDescriptor )
                    else if (local_header.Flags.HasFlag(HeaderFlags.UsePostDataDescriptor))
                    {
                        bool isRecording = rewindableStream.IsRecording;
                        if (!isRecording)
                        {
                            rewindableStream.StartRecording();
                        }
                        uint nextHeaderBytes = reader.ReadUInt32();

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
}