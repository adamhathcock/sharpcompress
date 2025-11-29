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
        IRarUnpack unpack;
        bool ownsUnpack;

        // For solid archives, use shared Unpack instance (must be processed sequentially)
        // For non-solid archives, create new instance per stream to support multi-threading
        if (archive.IsSolid)
        {
            unpack = IsRarV3 ? archive.UnpackV1.Value : archive.UnpackV2017.Value;
            ownsUnpack = false;
        }
        else
        {
            unpack = IsRarV3
                ? new Compressors.Rar.UnpackV1.Unpack()
                : new Compressors.Rar.UnpackV2017.Unpack();
            ownsUnpack = true;
        }

        var stream = new RarStream(
            unpack,
            FileHeader,
            new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive),
            ownsUnpack
        );

        stream.Initialize();
        return stream;
    }

    public async Task<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
    {
        IRarUnpack unpack;
        bool ownsUnpack;

        // For solid archives, use shared Unpack instance (must be processed sequentially)
        // For non-solid archives, create new instance per stream to support multi-threading
        if (archive.IsSolid)
        {
            unpack = IsRarV3 ? archive.UnpackV1.Value : archive.UnpackV2017.Value;
            ownsUnpack = false;
        }
        else
        {
            unpack = IsRarV3
                ? new Compressors.Rar.UnpackV1.Unpack()
                : new Compressors.Rar.UnpackV2017.Unpack();
            ownsUnpack = true;
        }

        var stream = new RarStream(
            unpack,
            FileHeader,
            new MultiVolumeReadOnlyStream(Parts.Cast<RarFilePart>(), archive),
            ownsUnpack
        );

        await stream.InitializeAsync(cancellationToken);
        return stream;
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
