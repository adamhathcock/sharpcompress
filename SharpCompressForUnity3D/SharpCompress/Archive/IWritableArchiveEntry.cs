namespace SharpCompress.Archive
{
    using System.IO;

    internal interface IWritableArchiveEntry
    {
        System.IO.Stream Stream { get; }
    }
}

