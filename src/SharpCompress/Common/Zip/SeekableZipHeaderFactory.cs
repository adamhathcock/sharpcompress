using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
    internal sealed class SeekableZipHeaderFactory : ZipHeaderFactory
    {
        private const int MINIMUM_EOCD_LENGTH = 22;
        private const int ZIP64_EOCD_LENGTH = 20;
        // Comment may be within 64kb + structure 22 bytes
        private const int MAX_SEARCH_LENGTH_FOR_EOCD = 65557;
        private bool _zip64;

        internal SeekableZipHeaderFactory(string? password, ArchiveEncoding archiveEncoding)
            : base(StreamingMode.Seekable, password, archiveEncoding)
        {
        }

        internal IEnumerable<ZipHeader> ReadSeekableHeader(Stream stream)
        {
            var reader = new BinaryReader(stream);

            SeekBackToHeader(stream, reader);

            var eocd_location = stream.Position;
            var entry = new DirectoryEndHeader();
            entry.Read(reader);

            if (entry.IsZip64)
            {
                _zip64 = true;

                // ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR should be before the EOCD
                stream.Seek(eocd_location - ZIP64_EOCD_LENGTH - 4, SeekOrigin.Begin);
                uint zip64_locator = reader.ReadUInt32();
                if( zip64_locator != ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR )
                {
                    throw new ArchiveException("Failed to locate the Zip64 Directory Locator");
                }

                var zip64Locator = new Zip64DirectoryEndLocatorHeader();
                zip64Locator.Read(reader);

                stream.Seek(zip64Locator.RelativeOffsetOfTheEndOfDirectoryRecord, SeekOrigin.Begin);
                uint zip64Signature = reader.ReadUInt32();
                if (zip64Signature != ZIP64_END_OF_CENTRAL_DIRECTORY)
                {
                    throw new ArchiveException("Failed to locate the Zip64 Header");
                }

                var zip64Entry = new Zip64DirectoryEndHeader();
                zip64Entry.Read(reader);
                stream.Seek(zip64Entry.DirectoryStartOffsetRelativeToDisk, SeekOrigin.Begin);
            }
            else
            {
                stream.Seek(entry.DirectoryStartOffsetRelativeToDisk, SeekOrigin.Begin);
            }

            long position = stream.Position;
            while (true)
            {
                stream.Position = position;
                uint signature = reader.ReadUInt32();
                var nextHeader = ReadHeader(signature, reader, _zip64);
                position = stream.Position;

                if (nextHeader is null)
                {
                    yield break;
                }

                if (nextHeader is DirectoryEntryHeader entryHeader)
                {
                    //entry could be zero bytes so we need to know that.
                    entryHeader.HasData = entryHeader.CompressedSize != 0;
                    yield return entryHeader;
                }
                else if (nextHeader is DirectoryEndHeader endHeader)
                {
                    yield return endHeader;
                }
            }
        }

        private static bool IsMatch( byte[] haystack, int position, byte[] needle)
        {
            for( int i = 0; i < needle.Length; i++ )
            {
                if( haystack[ position + i ] != needle[ i ] )
                {
                    return false;
                }
            }

            return true;
        }
        private static void SeekBackToHeader(Stream stream, BinaryReader reader)
        {
            // Minimum EOCD length
            if (stream.Length < MINIMUM_EOCD_LENGTH)
            {
                throw new ArchiveException("Could not find Zip file Directory at the end of the file. File may be corrupted.");
            }

            int len = stream.Length < MAX_SEARCH_LENGTH_FOR_EOCD ? (int)stream.Length : MAX_SEARCH_LENGTH_FOR_EOCD;
            // We search for marker in reverse to find the first occurance
            byte[] needle = { 0x06, 0x05, 0x4b, 0x50 };

            stream.Seek(-len, SeekOrigin.End);

            byte[] seek = reader.ReadBytes(len);

            // Search in reverse
            Array.Reverse(seek);

            var max_search_area = len - MINIMUM_EOCD_LENGTH;

            for( int pos_from_end = 0; pos_from_end < max_search_area; ++pos_from_end)
            {
                if( IsMatch(seek, pos_from_end, needle) )
                {
                    stream.Seek(-pos_from_end, SeekOrigin.End);
                    return;
                }
            }

            throw new ArchiveException("Failed to locate the Zip Header");
        }

        internal LocalEntryHeader GetLocalHeader(Stream stream, DirectoryEntryHeader directoryEntryHeader)
        {
            stream.Seek(directoryEntryHeader.RelativeOffsetOfEntryHeader, SeekOrigin.Begin);
            BinaryReader reader = new BinaryReader(stream);
            uint signature = reader.ReadUInt32();
            var localEntryHeader = ReadHeader(signature, reader, _zip64) as LocalEntryHeader;
            if (localEntryHeader is null)
            {
                throw new InvalidOperationException();
            }
            return localEntryHeader;
        }
    }
}