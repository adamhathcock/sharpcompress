namespace SharpCompress.Archives;

/// <summary>
/// Decorator for <see cref="Factories.Factory"/> used to declare an archive format as able to create writeable archives
/// </summary>
/// <remarks>
/// Implemented by:<br/>
/// <list type="table">
/// <item><see cref="Factories.TarFactory"/></item>
/// <item><see cref="Factories.ZipFactory"/></item>
/// <item><see cref="Factories.GZipFactory"/></item>
/// </list>
public interface IWriteableArchiveFactory : Factories.IFactory
{
    /// <summary>
    /// Creates a new, empty archive, ready to be written.
    /// </summary>
    /// <returns></returns>
    IWritableArchive CreateWriteableArchive();
}
