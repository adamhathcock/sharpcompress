using System.IO;
using SharpCompress.Factories;

namespace SharpCompress.Writers;

public interface IWriterFactory : IFactory
{
    IWriter OpenWriter(Stream stream, WriterOptions writerOptions);

    IAsyncWriter OpenAsyncWriter(Stream stream, WriterOptions writerOptions);
}
