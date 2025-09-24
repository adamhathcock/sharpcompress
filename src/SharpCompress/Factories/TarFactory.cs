using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Lzw;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Tar;
using SharpCompress.Writers;
using SharpCompress.Writers.Tar;
using GZipArchive = SharpCompress.Archives.GZip.GZipArchive;

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
        foreach (var testOption in compressionOptions)
        {
            foreach (var ext in testOption.KnownExtensions)
            {
                yield return ext;
            }
        }
    }

    /// <inheritdoc/>
    public override bool IsArchive(
        Stream stream,
        string? password = null,
        int bufferSize = ReaderOptions.DefaultBufferSize
    ) => TarArchive.IsTarFile(stream);

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


    protected class TestOption
    {
        public readonly CompressionType Type;
        public readonly Func<Stream, bool> CanHandle;
        public readonly bool WrapInSharpCompressStream;

        public readonly Func<Stream, Stream> CreateStream;

        public readonly IEnumerable<string> KnownExtensions;

        public TestOption(
            CompressionType Type,
            Func<Stream, bool> CanHandle,
            Func<Stream, Stream> CreateStream,
            IEnumerable<string> KnownExtensions,
            bool WrapInSharpCompressStream = true
        )
        {
            this.Type = Type;
            this.CanHandle = CanHandle;
            this.WrapInSharpCompressStream = WrapInSharpCompressStream;
            this.CreateStream = CreateStream;
            this.KnownExtensions = KnownExtensions;
        }
    }

    // https://en.wikipedia.org/wiki/Tar_(computing)#Suffixes_for_compressed_files
    protected TestOption[] compressionOptions =
    [
        new(CompressionType.None, (stream) => true, (stream) => stream, ["tar"], false), // We always do a test for IsTarFile later
        new(
            CompressionType.BZip2,
            BZip2Stream.IsBZip2,
            (stream) => new BZip2Stream(stream, CompressionMode.Decompress, false),
            ["tar.bz2", "tb2", "tbz", "tbz2", "tz2"]
        ),
        new(
            CompressionType.GZip,
            GZipArchive.IsGZipFile,
            (stream) => new GZipStream(stream, CompressionMode.Decompress),
            ["tar.gz", "taz", "tgz"]
        ),
        new(
            CompressionType.ZStandard,
            ZStandardStream.IsZStandard,
            (stream) => new ZStandardStream(stream),
            ["tar.zst", "tar.zstd", "tzst", "tzstd"]
        ),
        new(
            CompressionType.LZip,
            LZipStream.IsLZipFile,
            (stream) => new LZipStream(stream, CompressionMode.Decompress),
            ["tar.lz"]
        ),
        new(
            CompressionType.Xz,
            XZStream.IsXZStream,
            (stream) => new XZStream(stream),
            ["tar.xz", "txz"],
            false
        ),
        new(
            CompressionType.Lzw,
            LzwStream.IsLzwStream,
            (stream) => new LzwStream(stream),
            ["tar.Z", "tZ", "taZ"],
            false
        ),
    ];

    /// <inheritdoc/>
    internal override bool TryOpenReader(
        SharpCompressStream rewindableStream,
        ReaderOptions options,
        out IReader? reader
    )
    {
        reader = null;
        long pos = ((IStreamStack)rewindableStream).GetPosition();
        TestOption? testedOption = null;
        if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
        {
            testedOption = compressionOptions.FirstOrDefault(a =>
                a.KnownExtensions.Contains(
                    options.ExtensionHint,
                    StringComparer.CurrentCultureIgnoreCase
                )
            );
            if (testedOption != null)
            {
                reader = TryOption(rewindableStream, options, pos, testedOption);
                if (reader != null)
                {
                    return true;
                }
            }
        }

        foreach (var testOption in compressionOptions)
        {
            if (testedOption == testOption)
            {
                continue; // Already tested above
            }
            ((IStreamStack)rewindableStream).StackSeek(pos);
            reader = TryOption(rewindableStream, options, pos, testOption);
            if (reader != null)
            {
                return true;
            }
        }

        return false;
    }

    private static IReader? TryOption(
        SharpCompressStream rewindableStream,
        ReaderOptions options,
        long pos,
        TestOption testOption
    )
    {
        if (testOption.CanHandle(rewindableStream))
        {
            ((IStreamStack)rewindableStream).StackSeek(pos);
            var inStream = rewindableStream;
            if (testOption.WrapInSharpCompressStream)
            {
                inStream = SharpCompressStream.Create(rewindableStream, leaveOpen: true);
            }
            var testStream = testOption.CreateStream(rewindableStream);

            if (TarArchive.IsTarFile(testStream))
            {
                ((IStreamStack)rewindableStream).StackSeek(pos);
                return new TarReader(rewindableStream, options, testOption.Type);
            }
        }

        return null;
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
