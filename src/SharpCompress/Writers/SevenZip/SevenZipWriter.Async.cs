using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.SevenZip;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Crypto;
using SharpCompress.IO;

namespace SharpCompress.Writers.SevenZip;

public partial class SevenZipWriter
{
    /// <summary>
    /// Asynchronously disposes the writer, finalizing the 7z archive.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }
        GC.SuppressFinalize(this);
        _isDisposed = true;

        if (!finalized)
        {
            finalized = true;
            await FinalizeArchiveAsync().ConfigureAwait(false);
        }
        OutputStream?.Dispose();
        // base.DisposeAsync() is a no-op since _isDisposed is already set
        await base.DisposeAsync().ConfigureAwait(false);
    }

    private async Task FinalizeArchiveAsync()
    {
        var output = OutputStream.NotNull();

        // Current position = end of packed data streams
        var endOfPackedData = output.Position;

        // Build the header structures
        var mainStreamsInfo = BuildStreamsInfo();
        var filesInfo = new SevenZipFilesInfoWriter { Entries = entries.ToArray() };

        // Write header to a temporary stream first
        using var headerStream = new MemoryStream();
        ArchiveHeaderWriter.WriteRawHeader(headerStream, mainStreamsInfo, filesInfo);

        // Optionally compress the header
        if (sevenZipOptions.CompressHeader && headerStream.Length > 0)
        {
            await WriteCompressedHeaderAsync(headerStream, endOfPackedData).ConfigureAwait(false);
        }
        else
        {
            await WriteRawHeaderToOutputAsync(headerStream, endOfPackedData).ConfigureAwait(false);
        }
    }

    private async Task WriteCompressedHeaderAsync(
        MemoryStream rawHeaderStream,
        long endOfPackedData
    )
    {
        var output = OutputStream.NotNull();

        // Compress header using LZMA (always LZMA, not LZMA2, matching 7-Zip standard behavior)
        rawHeaderStream.Position = 0;
        var headerCompressor = new SevenZipStreamsCompressor(output);
        var headerPacked = await headerCompressor
            .CompressAsync(rawHeaderStream, CompressionType.LZMA, sevenZipOptions.LzmaProperties)
            .ConfigureAwait(false);

        // Build EncodedHeader StreamsInfo (describes how to decompress the header)
        var headerPackPos = (ulong)(endOfPackedData - SevenZipSignatureHeaderWriter.HeaderSize);
        var headerStreamsInfo = new SevenZipStreamsInfoWriter
        {
            PackInfo = new SevenZipPackInfoWriter
            {
                PackPos = headerPackPos,
                Sizes = headerPacked.Sizes,
                CRCs = headerPacked.CRCs,
            },
            UnPackInfo = new SevenZipUnPackInfoWriter { Folders = [headerPacked.Folder] },
        };

        // Write encoded header to a second temporary stream
        using var encodedHeaderStream = new MemoryStream();
        ArchiveHeaderWriter.WriteEncodedHeader(encodedHeaderStream, headerStreamsInfo);

        // Write the encoded header to the output
        var headerStartPos = output.Position;
        encodedHeaderStream.Position = 0;
        await encodedHeaderStream.CopyToAsync(output).ConfigureAwait(false);

        // Compute CRC of the encoded header
        var headerCrc = Crc32Stream.Compute(
            Crc32Stream.DEFAULT_POLYNOMIAL,
            Crc32Stream.DEFAULT_SEED,
            encodedHeaderStream.GetBuffer().AsSpan(0, (int)encodedHeaderStream.Length)
        );

        // Back-patch signature header
        var nextHeaderOffset = (ulong)(headerStartPos - SevenZipSignatureHeaderWriter.HeaderSize);
        var nextHeaderSize = (ulong)encodedHeaderStream.Length;

        await SevenZipSignatureHeaderWriter
            .WriteFinalAsync(output, nextHeaderOffset, nextHeaderSize, headerCrc)
            .ConfigureAwait(false);

        // Seek to end
        output.Seek(0, SeekOrigin.End);
    }

    private async Task WriteRawHeaderToOutputAsync(
        MemoryStream rawHeaderStream,
        long endOfPackedData
    )
    {
        var output = OutputStream.NotNull();

        // Write raw header directly
        var headerStartPos = output.Position;
        rawHeaderStream.Position = 0;
        await rawHeaderStream.CopyToAsync(output).ConfigureAwait(false);

        // Compute CRC of the raw header
        var headerCrc = Crc32Stream.Compute(
            Crc32Stream.DEFAULT_POLYNOMIAL,
            Crc32Stream.DEFAULT_SEED,
            rawHeaderStream.GetBuffer().AsSpan(0, (int)rawHeaderStream.Length)
        );

        // Back-patch signature header
        var nextHeaderOffset = (ulong)(headerStartPos - SevenZipSignatureHeaderWriter.HeaderSize);
        var nextHeaderSize = (ulong)rawHeaderStream.Length;

        await SevenZipSignatureHeaderWriter
            .WriteFinalAsync(output, nextHeaderOffset, nextHeaderSize, headerCrc)
            .ConfigureAwait(false);

        // Seek to end
        output.Seek(0, SeekOrigin.End);
    }

    /// <summary>
    /// Asynchronously writes a file entry to the 7z archive.
    /// </summary>
    public override async ValueTask WriteAsync(
        string filename,
        Stream source,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        if (finalized)
        {
            throw new ObjectDisposedException(
                nameof(SevenZipWriter),
                "Cannot write to a finalized archive."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();
        await EnsurePlaceholderWrittenAsync(cancellationToken).ConfigureAwait(false);

        filename = NormalizeFilename(filename);
        var progressStream = WrapWithProgress(source, filename);

        var isEmpty = source.CanSeek && source.Length == 0;

        if (isEmpty)
        {
            entries.Add(
                new SevenZipWriteEntry
                {
                    Name = filename,
                    ModificationTime = modificationTime,
                    IsDirectory = false,
                    IsEmpty = true,
                }
            );
            return;
        }

        var output = OutputStream.NotNull();
        var outputPosBefore = output.Position;
        var compressor = new SevenZipStreamsCompressor(output);
        var packed = await compressor
            .CompressAsync(
                progressStream,
                sevenZipOptions.CompressionType,
                sevenZipOptions.LzmaProperties,
                cancellationToken
            )
            .ConfigureAwait(false);

        var actuallyEmpty = packed.Folder.GetUnpackSize() == 0;
        if (!actuallyEmpty)
        {
            packedStreams.Add(packed);
        }
        else
        {
            output.Position = outputPosBefore;
            output.SetLength(outputPosBefore);
        }

        entries.Add(
            new SevenZipWriteEntry
            {
                Name = filename,
                ModificationTime = modificationTime,
                IsDirectory = false,
                IsEmpty = isEmpty || actuallyEmpty,
            }
        );
    }

    /// <summary>
    /// Asynchronously writes a directory entry to the 7z archive.
    /// </summary>
    public override ValueTask WriteDirectoryAsync(
        string directoryName,
        DateTime? modificationTime,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteDirectory(directoryName, modificationTime);
        return new ValueTask();
    }
}
