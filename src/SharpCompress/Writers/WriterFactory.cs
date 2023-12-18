using System;
using System.IO;
using System.Linq;
using SharpCompress.Common;

namespace SharpCompress.Writers;

public static class WriterFactory
{
    public static IWriter Open(Stream stream, ArchiveType archiveType, WriterOptions writerOptions)
    {
        var factory = Factories
            .Factory.Factories.OfType<IWriterFactory>()
            .FirstOrDefault(item => item.KnownArchiveType == archiveType);

        if (factory != null)
        {
            return factory.Open(stream, writerOptions);
        }

        throw new NotSupportedException("Archive Type does not have a Writer: " + archiveType);
    }
}
