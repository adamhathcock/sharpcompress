using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
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

        internal async IAsyncEnumerable<ZipHeader> ReadStreamHeader(RewindableStream rewindableStream, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                ZipHeader? header;
                if (_lastEntryHeader != null &&
                    (FlagUtility.HasFlag(_lastEntryHeader.Flags, HeaderFlags.UsePostDataDescriptor) || _lastEntryHeader.IsZip64))
                {
                    await ((StreamingZipFilePart)_lastEntryHeader.Part).FixStreamedFileLocation(rewindableStream, cancellationToken);
                    long? pos = rewindableStream.CanSeek ? (long?)rewindableStream.Position : null;
                    uint crc = await rewindableStream.ReadUInt32(cancellationToken);
                    if (crc == POST_DATA_DESCRIPTOR)
                    {
                        crc = await rewindableStream.ReadUInt32(cancellationToken);
                    }
                    _lastEntryHeader.Crc = crc;
                    _lastEntryHeader.CompressedSize = await rewindableStream.ReadUInt32(cancellationToken);
                    _lastEntryHeader.UncompressedSize = await rewindableStream.ReadUInt32(cancellationToken);
                    if (pos.HasValue)
                    {
                        _lastEntryHeader.DataStartPosition = pos - _lastEntryHeader.CompressedSize;
                    }
                }
                _lastEntryHeader = null;
                var headerBytes = await rewindableStream.ReadUInt32OrNull(cancellationToken);
                if (headerBytes is null)
                {
                    yield break;
                }
                header = await ReadHeader(headerBytes.Value, rewindableStream, cancellationToken);
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
                        uint nextHeaderBytes = await rewindableStream.ReadUInt32(cancellationToken);

                        // Check if next data is PostDataDescriptor, streamed file with 0 length
                        header.HasData = nextHeaderBytes != POST_DATA_DESCRIPTOR;
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