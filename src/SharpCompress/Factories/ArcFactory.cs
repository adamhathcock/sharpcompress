using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arc;
using static System.Net.Mime.MediaTypeNames;

namespace SharpCompress.Factories;

public class ArcFactory : Factory, IReaderFactory
{
    public override string Name => "Arc";

    public override ArchiveType? KnownArchiveType => ArchiveType.Arc;

    public override IEnumerable<string> GetSupportedExtensions()
    {
        yield return "arc";
    }

    public override bool IsArchive(Stream stream, string? password = null)
    {
        //You may have to use some(paranoid) checks to ensure that you actually are
        //processing an ARC file, since other archivers also adopted the idea of putting
        //a 01Ah byte at offset 0, namely the Hyper archiver. To check if you have a
        //Hyper - archive, check the next two bytes for "HP" or "ST"(or look below for
        //"HYP").Also the ZOO archiver also does put a 01Ah at the start of the file,
        //see the ZOO entry below.
        return stream.TryWithRentedBufferReadFully(
                2,
                buffer => buffer[0] == 0x1A && buffer[1] < 10, //rather thin, but this is all we have
                out var result
            ) && result;
    }

    public IReader OpenReader(Stream stream, ReaderOptions? options) =>
        ArcReader.OpenReader(stream, options);

    public ValueTask<IAsyncReader> OpenAsyncReader(
        Stream stream,
        ReaderOptions? options,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new((IAsyncReader)ArcReader.OpenReader(stream, options));
    }

    public override async ValueTask<bool> IsArchiveAsync(
        Stream stream,
        string? password = null,
        CancellationToken cancellationToken = default
    )
    {
        //You may have to use some(paranoid) checks to ensure that you actually are
        //processing an ARC file, since other archivers also adopted the idea of putting
        //a 01Ah byte at offset 0, namely the Hyper archiver. To check if you have a
        //Hyper - archive, check the next two bytes for "HP" or "ST"(or look below for
        //"HYP").Also the ZOO archiver also does put a 01Ah at the start of the file,
        //see the ZOO entry below.
        return await stream.WithRentedBufferReadExactAsync(
            2,
            buffer => buffer[0] == 0x1A && buffer[1] < 10, //rather thin, but this is all we have
            cancellationToken
        );
    }
}
