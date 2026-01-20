using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

public static class Extractor
{
    private const char Delimiter = '/';

    public static IEnumerable<string> GetFiles(string filename)
    {
        IArchive? archive = null;
        FileStream? fileStream = null;

        try
        {
            fileStream = File.OpenRead(filename);
            archive = ArchiveFactory.OpenArchive(fileStream);

            if (archive is null)
            {
                return [];
            }

            IReader reader = archive.ExtractAllEntries();
            var liveries = new List<string>();

            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory)
                    continue;

                var entryKey = reader.Entry.Key;

                if (entryKey is null)
                    continue;

                 using EntryStream source = reader.OpenEntryStream();
                 using MemoryStream memoryStream = new();
                 source.CopyTo(memoryStream);
                var data = memoryStream.ToArray();

                var nameParts = entryKey.Split(Delimiter);
            }

            return liveries;
        }
        finally
        {
            if (fileStream is not null)
            {
                 fileStream.Dispose();
            }
            archive?.Dispose();
        }
    }
}
