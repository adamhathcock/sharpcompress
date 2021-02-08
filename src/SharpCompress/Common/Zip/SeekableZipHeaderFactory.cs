using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Xz;
using SharpCompress.IO;

namespace SharpCompress.Common.Zip
{
    internal sealed class SeekableZipHeaderFactory : ZipHeaderFactory
    {
        private const int MAX_ITERATIONS_FOR_DIRECTORY_HEADER = 4096;
        private bool _zip64;

        internal SeekableZipHeaderFactory(string? password, ArchiveEncoding archiveEncoding)
            : base(StreamingMode.Seekable, password, archiveEncoding)
        {
        }

        internal async IAsyncEnumerable<ZipHeader> ReadSeekableHeader(Stream stream, [EnumeratorCancellation]CancellationToken cancellationToken)
        {
            await SeekBackToHeader(stream, DIRECTORY_END_HEADER_BYTES, cancellationToken);
            var entry = new DirectoryEndHeader();
            await entry.Read(stream, cancellationToken);

            if (entry.IsZip64)
            {
                _zip64 = true;
                await SeekBackToHeader(stream, ZIP64_END_OF_CENTRAL_DIRECTORY_LOCATOR, cancellationToken);
                var zip64Locator = new Zip64DirectoryEndLocatorHeader();
                await zip64Locator.Read(stream, cancellationToken);

                stream.Seek(zip64Locator.RelativeOffsetOfTheEndOfDirectoryRecord, SeekOrigin.Begin);
                uint zip64Signature = await stream.ReadUInt32(cancellationToken);
                if (zip64Signature != ZIP64_END_OF_CENTRAL_DIRECTORY)
                {
                    throw new ArchiveException("Failed to locate the Zip64 Header");
                }

                var zip64Entry = new Zip64DirectoryEndHeader();
                await zip64Entry.Read(stream, cancellationToken);
                stream.Seek(zip64Entry.DirectoryStartOffsetRelativeToDisk, SeekOrigin.Begin);
            }
            else
            {
                stream.Seek(entry.DirectoryStartOffsetRelativeToDisk ?? 0, SeekOrigin.Begin);
            }

            long position = stream.Position;
            while (true)
            {
                stream.Position = position;
                uint signature = await stream.ReadUInt32(cancellationToken);
                var nextHeader = await ReadHeader(signature, stream, cancellationToken, _zip64);
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

        private static async ValueTask SeekBackToHeader(Stream stream, uint headerSignature, CancellationToken cancellationToken)
        {
            long offset = 0;
            uint signature;
            int iterationCount = 0;
            do
            {
                if ((stream.Length + offset) - 4 < 0)
                {
                    throw new ArchiveException("Failed to locate the Zip Header");
                }
                stream.Seek(offset - 4, SeekOrigin.End);
                signature = await stream.ReadLittleEndianUInt32(cancellationToken);
                offset--;
                iterationCount++;
                if (iterationCount > MAX_ITERATIONS_FOR_DIRECTORY_HEADER)
                {
                    throw new ArchiveException("Could not find Zip file Directory at the end of the file.  File may be corrupted.");
                }
            }
            while (signature != headerSignature);
        }

        internal async ValueTask<LocalEntryHeader> GetLocalHeader(Stream stream, DirectoryEntryHeader directoryEntryHeader, CancellationToken cancellationToken)
        {
            stream.Seek(directoryEntryHeader.RelativeOffsetOfEntryHeader, SeekOrigin.Begin);
            uint signature = await stream.ReadUInt32(cancellationToken);
            var localEntryHeader = await ReadHeader(signature, stream, cancellationToken, _zip64) as LocalEntryHeader;
            if (localEntryHeader is null)
            {
                throw new InvalidOperationException();
            }
            return localEntryHeader;
        }
    }
}