using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Common.Zip;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Compressors.Deflate;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Readers.Zip;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace SharpCompress.Archives.Zip;

public class ZipArchive : AbstractWritableArchive<ZipArchiveEntry, ZipVolume>
{
    private readonly SeekableZipHeaderFactory? headerFactory;

    /// <summary>
    /// Gets or sets the compression level applied to files added to the archive,
    /// if the compression method is set to deflate
    /// </summary>
    public CompressionLevel DeflateCompressionLevel { get; set; }

    /// <summary>
    /// Constructor with a SourceStream able to handle FileInfo and Streams.
    /// </summary>
    /// <param name="sourceStream"></param>
    /// <param name="options"></param>
    internal ZipArchive(SourceStream sourceStream)
        : base(ArchiveType.Zip, sourceStream) =>
        headerFactory = new SeekableZipHeaderFactory(
            sourceStream.ReaderOptions.Password,
            sourceStream.ReaderOptions.ArchiveEncoding
        );

    /// <summary>
    /// Constructor expects a filepath to an existing file.
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="readerOptions"></param>
    public static ZipArchive Open(string filePath, ReaderOptions? readerOptions = null)
    {
        filePath.CheckNotNullOrEmpty(nameof(filePath));
        return Open(new FileInfo(filePath), readerOptions ?? new ReaderOptions());
    }

    /// <summary>
    /// Constructor with a FileInfo object to an existing file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="readerOptions"></param>
    public static ZipArchive Open(FileInfo fileInfo, ReaderOptions? readerOptions = null)
    {
        fileInfo.CheckNotNull(nameof(fileInfo));
        return new ZipArchive(
            new SourceStream(
                fileInfo,
                i => ZipArchiveVolumeFactory.GetFilePart(i, fileInfo),
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all file parts passed in
    /// </summary>
    /// <param name="fileInfos"></param>
    /// <param name="readerOptions"></param>
    public static ZipArchive Open(
        IEnumerable<FileInfo> fileInfos,
        ReaderOptions? readerOptions = null
    )
    {
        fileInfos.CheckNotNull(nameof(fileInfos));
        var files = fileInfos.ToArray();
        return new ZipArchive(
            new SourceStream(
                files[0],
                i => i < files.Length ? files[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Constructor with all stream parts passed in
    /// </summary>
    /// <param name="streams"></param>
    /// <param name="readerOptions"></param>
    public static ZipArchive Open(IEnumerable<Stream> streams, ReaderOptions? readerOptions = null)
    {
        streams.CheckNotNull(nameof(streams));
        var strms = streams.ToArray();
        return new ZipArchive(
            new SourceStream(
                strms[0],
                i => i < strms.Length ? strms[i] : null,
                readerOptions ?? new ReaderOptions()
            )
        );
    }

    /// <summary>
    /// Takes a seekable Stream as a source
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="readerOptions"></param>
    public static ZipArchive Open(Stream stream, ReaderOptions? readerOptions = null)
    {
        stream.CheckNotNull(nameof(stream));
        return new ZipArchive(
            new SourceStream(stream, i => null, readerOptions ?? new ReaderOptions())
        );
    }

    public static bool IsZipFile(string filePath, string? password = null) =>
        IsZipFile(new FileInfo(filePath), password);

    public static bool IsZipFile(FileInfo fileInfo, string? password = null)
    {
        if (!fileInfo.Exists)
        {
            return false;
        }
        using Stream stream = fileInfo.OpenRead();
        return IsZipFile(stream, password);
    }

    public static bool IsZipFile(Stream stream, string? password = null)
    {
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            var header = headerFactory
                .ReadStreamHeader(stream)
                .FirstOrDefault(x => x.ZipHeaderType != ZipHeaderType.Split);
            if (header is null)
            {
                return false;
            }
            return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
        }
        catch (CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsZipMulti(Stream stream, string? password = null)
    {
        var headerFactory = new StreamingZipHeaderFactory(password, new ArchiveEncoding(), null);
        try
        {
            var header = headerFactory
                .ReadStreamHeader(stream)
                .FirstOrDefault(x => x.ZipHeaderType != ZipHeaderType.Split);
            if (header is null)
            {
                if (stream.CanSeek) //could be multipart. Test for central directory - might not be z64 safe
                {
                    var z = new SeekableZipHeaderFactory(password, new ArchiveEncoding());
                    var x = z.ReadSeekableHeader(stream).FirstOrDefault();
                    return x?.ZipHeaderType == ZipHeaderType.DirectoryEntry;
                }
                else
                {
                    return false;
                }
            }
            return Enum.IsDefined(typeof(ZipHeaderType), header.ZipHeaderType);
        }
        catch (CryptographicException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    protected override IEnumerable<ZipVolume> LoadVolumes(SourceStream stream)
    {
        stream.LoadAllParts(); //request all streams
        stream.Position = 0;

        var streams = stream.Streams.ToList();
        var idx = 0;
        if (streams.Count() > 1) //test part 2 - true = multipart not split
        {
            streams[1].Position += 4; //skip the POST_DATA_DESCRIPTOR to prevent an exception
            var isZip = IsZipFile(streams[1], ReaderOptions.Password);
            streams[1].Position -= 4;
            if (isZip)
            {
                stream.IsVolumes = true;

                var tmp = streams[0]; //arcs as zip, z01 ... swap the zip the end
                streams.RemoveAt(0);
                streams.Add(tmp);

                //streams[0].Position = 4; //skip the POST_DATA_DESCRIPTOR to prevent an exception
                return streams.Select(a => new ZipVolume(a, ReaderOptions, idx++));
            }
        }

        //split mode or single file
        return new ZipVolume(stream, ReaderOptions, idx++).AsEnumerable();
    }

    internal ZipArchive()
        : base(ArchiveType.Zip) { }

    protected override IEnumerable<ZipArchiveEntry> LoadEntries(IEnumerable<ZipVolume> volumes)
    {
        var vols = volumes.ToArray();
        foreach (var h in headerFactory.NotNull().ReadSeekableHeader(vols.Last().Stream))
        {
            if (h != null)
            {
                switch (h.ZipHeaderType)
                {
                    case ZipHeaderType.DirectoryEntry:
                        {
                            var deh = (DirectoryEntryHeader)h;
                            Stream s;
                            if (
                                deh.RelativeOffsetOfEntryHeader + deh.CompressedSize
                                > vols[deh.DiskNumberStart].Stream.Length
                            )
                            {
                                var v = vols.Skip(deh.DiskNumberStart).ToArray();
                                s = new SourceStream(
                                    v[0].Stream,
                                    i => i < v.Length ? v[i].Stream : null,
                                    new ReaderOptions() { LeaveStreamOpen = true }
                                );
                            }
                            else
                            {
                                s = vols[deh.DiskNumberStart].Stream;
                            }

                            yield return new ZipArchiveEntry(
                                this,
                                new SeekableZipFilePart(headerFactory.NotNull(), deh, s)
                            );
                        }
                        break;
                    case ZipHeaderType.DirectoryEnd:
                    {
                        var bytes = ((DirectoryEndHeader)h).Comment ?? Array.Empty<byte>();
                        vols.Last().Comment = ReaderOptions.ArchiveEncoding.Decode(bytes);
                        yield break;
                    }
                }
            }
        }
    }

    public void SaveTo(Stream stream) => SaveTo(stream, new WriterOptions(CompressionType.Deflate));

    protected override void SaveTo(
        Stream stream,
        WriterOptions options,
        IEnumerable<ZipArchiveEntry> oldEntries,
        IEnumerable<ZipArchiveEntry> newEntries
    )
    {
        using var writer = new ZipWriter(stream, new ZipWriterOptions(options));
        foreach (var entry in oldEntries.Concat(newEntries).Where(x => !x.IsDirectory))
        {
            using var entryStream = entry.OpenEntryStream();
            writer.Write(
                entry.Key.NotNull("Entry Key is null"),
                entryStream,
                entry.LastModifiedTime
            );
        }
    }

    protected override ZipArchiveEntry CreateEntryInternal(
        string filePath,
        Stream source,
        long size,
        DateTime? modified,
        bool closeStream
    ) => new ZipWritableArchiveEntry(this, source, filePath, size, modified, closeStream);

    public static ZipArchive Create() => new();

    protected override IReader CreateReaderForSolidExtraction()
    {
        var stream = Volumes.Single().Stream;
        stream.Position = 0;
        return ZipReader.Open(stream, ReaderOptions, Entries);
    }
}
