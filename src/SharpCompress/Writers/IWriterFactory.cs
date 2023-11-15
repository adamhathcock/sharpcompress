using System.IO;
using SharpCompress.Factories;

namespace SharpCompress.Writers;

public interface IWriterFactory : IFactory
{
    IWriter Open(Stream stream, WriterOptions writerOptions);
}
