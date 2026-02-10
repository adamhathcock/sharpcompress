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

    private readonly struct ArcSignatureProcessor : Utility.IBufferProcessor<bool>
    {
        public bool Process(ReadOnlySpan<byte> buffer) => buffer[0] == 0x1A && buffer[1] < 10;
    }

    private readonly struct ArcSignatureTryProcessor : Utility.ITryBufferProcessor<(bool, bool)>
    {
        public (bool, bool) OnSuccess(ReadOnlySpan<byte> buffer) =>
            (true, buffer[0] == 0x1A && buffer[1] < 10);

        public (bool, bool) OnFailure() => (false, false);
    }

    public override bool IsArchive(Stream stream, string? password = null)
    {
        //You may have to use some(paranoid) checks to ensure that you actually are
        //processing an ARC file, since other archivers also adopted the idea of putting
        //a 01Ah byte at offset 0, namely the Hyper archiver. To check if you have a
        //Hyper - archive, check the next two bytes for "HP" or "ST"(or look below for
        //"HYP").Also the ZOO archiver also does put a 01Ah at the start of the file,
        //see the ZOO entry below.
        var processor = new ArcSignatureTryProcessor();
        var result = stream.TryReadFullyRented<
            ArcSignatureTryProcessor,
            (bool success, bool match)
        >(2, ref processor);
        return result.success && result.match;
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
        var processor = new ArcSignatureProcessor();
        return await stream.ReadExactRentedAsync<ArcSignatureProcessor, bool>(
            2,
            processor,
            cancellationToken
        );
    }
}
