using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Common.Rar;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Compressors.Rar;
using SharpCompress.Readers;

namespace SharpCompress.Archives.Rar;

public class RarArchiveEntry : RarEntry, IArchiveEntry
{
    private readonly ICollection<RarFilePart> parts;
    private readonly RarArchive archive;
    private readonly ReaderOptions readerOptions;

    internal RarArchiveEntry(
        RarArchive archive,
        IEnumerable<RarFilePart> parts,
        ReaderOptions readerOptions
    )
    {
        this.parts = parts.ToList();
        this.archive = archive;
        this.readerOptions = readerOptions;
        IsSolid = FileHeader.IsSolid;
    }

    public override CompressionType CompressionType => CompressionType.Rar;

    public IArchive Archive => archive;

    internal override IEnumerable<FilePart> Parts => parts.Cast<FilePart>();

    internal override FileHeader FileHeader => parts.First().FileHeader;

    public override long Crc
    {
        get
        {
            CheckIncomplete();
            return BitConverter.ToUInt32(
                parts.Select(fp => fp.FileHeader).Single(fh => !fh.IsSplitAfter).FileCrc.NotNull(),
                0
            );
        }
    }

    public override long Size
    {
        get
        {
            CheckIncomplete();
            return parts.First().FileHeader.UncompressedSize;
        }
    }

    public override long CompressedSize
    {
        get
        {
            CheckIncomplete();
            return parts.Aggregate(0L, (total, fp) => total + fp.FileHeader.CompressedSize);
        }
    }

    public Stream OpenEntryStream()
    {
        var readStream = new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive);

        // For solid archives, use shared Unpack instance (must be processed sequentially)
        // For non-solid archives, use factory to create owned instance (supports multi-threading)
        if (archive.IsSolid)
        {
            var unpack = IsRarV3 ? archive.UnpackV1.Value : archive.UnpackV2017.Value;
            var stream = new RarStream(unpack, FileHeader, readStream, ownsUnpack: false);
            stream.Initialize();
            return stream;
        }
        else
        {
            var factory = IsRarV3
                ? (IRarUnpackFactory)UnpackV1Factory.Instance
                : UnpackV2017Factory.Instance;
            var stream = new RarStream(factory, FileHeader, readStream);
            stream.Initialize();
            return stream;
        }
    }

    public async Task<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
    {
        var readStream = new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive);

        // For solid archives, use shared Unpack instance (must be processed sequentially)
        // For non-solid archives, use factory to create owned instance (supports multi-threading)
        if (archive.IsSolid)
        {
            var unpack = IsRarV3 ? archive.UnpackV1.Value : archive.UnpackV2017.Value;
            var stream = new RarStream(unpack, FileHeader, readStream, ownsUnpack: false);
            await stream.InitializeAsync(cancellationToken);
            return stream;
        }
        else
        {
            var factory = IsRarV3
                ? (IRarUnpackFactory)UnpackV1Factory.Instance
                : UnpackV2017Factory.Instance;
            var stream = new RarStream(factory, FileHeader, readStream);
            await stream.InitializeAsync(cancellationToken);
            return stream;
        }
    }

    public bool IsComplete
    {
        get
        {
            var headers = parts.Select(x => x.FileHeader);
            return !headers.First().IsSplitBefore && !headers.Last().IsSplitAfter;
        }
    }

    private void CheckIncomplete()
    {
        if (!readerOptions.DisableCheckIncomplete && !IsComplete)
        {
            throw new IncompleteArchiveException(
                "ArchiveEntry is incomplete and cannot perform this operation."
            );
        }
    }

    public override bool SupportsMultiThreading =>
        !archive.IsSolid && Parts.Single().SupportsMultiThreading;
}
