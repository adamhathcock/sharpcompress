using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Readers.Arc;
using static System.Net.Mime.MediaTypeNames;

namespace SharpCompress.Factories
{
    public class ArcFactory : Factory, IReaderFactory
    {
        public override string Name => "Arc";

        public override ArchiveType? KnownArchiveType => ArchiveType.Arc;

        public override IEnumerable<string> GetSupportedExtensions()
        {
            yield return "arc";
        }

        public override bool IsArchive(
            Stream stream,
            string? password = null,
            int bufferSize = ReaderOptions.DefaultBufferSize
        )
        {
            //You may have to use some(paranoid) checks to ensure that you actually are
            //processing an ARC file, since other archivers also adopted the idea of putting
            //a 01Ah byte at offset 0, namely the Hyper archiver. To check if you have a
            //Hyper - archive, check the next two bytes for "HP" or "ST"(or look below for
            //"HYP").Also the ZOO archiver also does put a 01Ah at the start of the file,
            //see the ZOO entry below.
            var bytes = new byte[2];
            stream.Read(bytes, 0, 2);
            return bytes[0] == 0x1A && bytes[1] < 10; //rather thin, but this is all we have
        }

        public IReader OpenReader(Stream stream, ReaderOptions? options) =>
            ArcReader.Open(stream, options);
    }
}
