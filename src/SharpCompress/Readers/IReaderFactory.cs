using System.IO;

namespace SharpCompress.Readers;

public interface IReaderFactory : Factories.IFactory
{
    /// <summary>
    /// Opens a Reader for Non-seeking usage
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    IReader OpenReader(Stream stream, ReaderOptions? options);
}
