namespace SharpCompress.Archives;

public interface IWriteableArchiveFactory : Factories.IFactory
{
    IWritableArchive CreateWriteableArchive();
}
