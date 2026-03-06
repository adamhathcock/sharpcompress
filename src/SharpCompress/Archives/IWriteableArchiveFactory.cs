using SharpCompress.Common.Options;

namespace SharpCompress.Archives;

/// <summary>
/// Decorator for <see cref="Factories.Factory"/> used to declare an archive format as able to create writable archives.
/// </summary>
/// <remarks>
/// Implemented by:<br/>
/// <list type="table">
/// <item><see cref="Factories.TarFactory"/></item>
/// <item><see cref="Factories.ZipFactory"/></item>
/// <item><see cref="Factories.GZipFactory"/></item>
/// </list>
/// </remarks>
public interface IWritableArchiveFactory<TOptions> : Factories.IFactory
    where TOptions : IWriterOptions
{
    /// <summary>
    /// Creates a new, empty archive, ready to be written.
    /// </summary>
    /// <returns></returns>
    IWritableArchive<TOptions> CreateArchive();
}
