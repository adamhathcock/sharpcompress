using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;

public static class Extractor
{
    private const char Delimiter = '/';

    public static async Task<IEnumerable<string>> GetFiles(string filename)
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

                await using EntryStream source = reader.OpenEntryStream();
                await using MemoryStream memoryStream = new();
                await source.CopyToAsync(memoryStream);
                var data = memoryStream.ToArray();

                var nameParts = entryKey.Split(Delimiter);
            }

            return liveries;
        }
        finally
        {
            if (fileStream is not null)
            {
                await fileStream.DisposeAsync();
            }
            archive?.Dispose();
        }
    }
}
