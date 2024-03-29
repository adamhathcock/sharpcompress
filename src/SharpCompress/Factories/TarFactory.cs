using System.Collections.Generic;
using System.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;

namespace SharpCompress.Factories;

/// <summary>
/// Represents the foundation factory of TAR archive.
/// </summary>
public class TarFactory
    : Factory,
        IArchiveFactory,
        IMultiArchiveFactory,
        IReaderFactory,
        IWriterFactory,
        IWriteableArchiveFactory
{
    #region IFactory

    /// <inheritdoc/>
    public override string Name => "Tar";

    /// <inheritdoc/>
    public override ArchiveType? KnownArchiveType => ArchiveType.Tar;

    /// <inheritdoc/>
    public override IEnumerable<string> GetSupportedExtensions()
    {
        // from https://en.wikipedia.org/wiki/Tar_(computing)#Suffixes_for_compressed_files

        yield return "tar";

        // gzip
        yield return "taz";
        yield return "tgz";

        // bzip2
        yield return "tb2";
        yield return "tbz";
        yield return "tbz2";
        yield return "tz2";

        // lzma
        // yield return "tlz"; // unsupported

        // xz
        // yield return "txz"; // unsupported

        // compress
        yield return "tZ";
        yield return "taZ";

        // zstd
        // yield return "tzst"; // unsupported
    }

    /// <inheritdoc/>
    public override bool IsArchive(Stream stream, string? password = null) =>
        TarArchive.IsTarFile(stream);

    #endregion

    #region IArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(Stream stream, ReaderOptions? readerOptions = null) =>
        TarArchive.Open(stream, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null) =>
        TarArchive.Open(fileInfo, readerOptions);

    #endregion

    #region IMultiArchiveFactory

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<Stream> streams, ReaderOptions? readerOptions = null) =>
        TarArchive.Open(streams, readerOptions);

    /// <inheritdoc/>
    public IArchive Open(IReadOnlyList<FileInfo> fileInfos, ReaderOptions? readerOptions = null) =>
        TarArchive.Open(fileInfos, readerOptions);

    #endregion

    #region IReaderFactory

    /// <inheritdoc/>
    internal override bool TryOpenReader(
        RewindableStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;

        rewindableStream.Rewind(false);
        if (TarArchive.IsTarFile(rewindableStream))
        {
            rewindableStream.Rewind(true);
            reader = OpenReader(rewindableStream, options);
            return true;
        }

        rewindableStream.Rewind(false);
        if (BZip2Stream.IsBZip2(rewindableStream))
        {
            rewindableStream.Rewind(false);
            var testStream = new BZip2Stream(
                NonDisposingStream.Create(rewindableStream),
                CompressionMode.Decompress,
                false
            );
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                reader = new TarReader(rewindableStream, options, CompressionType.BZip2);
                return true;
            }
        }

        rewindableStream.Rewind(false);
        if (LZipStream.IsLZipFile(rewindableStream))
        {
            rewindableStream.Rewind(false);
            var testStream = new LZipStream(
                NonDisposingStream.Create(rewindableStream),
                CompressionMode.Decompress
            );
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                reader = new TarReader(rewindableStream, options, CompressionType.LZip);
                return true;
            }
        }

        rewindableStream.Rewind(false);
        if (XZStream.IsXZStream(rewindableStream))
        {
            rewindableStream.Rewind(true);
            var testStream = new XZStream(rewindableStream);
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                reader = new TarReader(rewindableStream, options, CompressionType.Xz);
                return true;
            }
        }

        rewindableStream.Rewind(false);
        if (LzwStream.IsLzwStream(rewindableStream))
        {
            rewindableStream.Rewind(false);
            var testStream = new LzwStream(rewindableStream);
            if (TarArchive.IsTarFile(testStream))
            {
                rewindableStream.Rewind(true);
                reader = new TarReader(rewindableStream, options, CompressionType.Lzw);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        TarReader.Open(stream, options);

    #endregion

    #region IWriterFactory

    /// <inheritdoc/>
    public IWriter Open(Stream stream, WriterOptions writerOptions) =>
        new TarWriter(stream, new TarWriterOptions(writerOptions));

    #endregion

    #region IWriteableArchiveFactory

    /// <inheritdoc/>
    public IWritableArchive CreateWriteableArchive() => TarArchive.Create();

    #endregion
}
