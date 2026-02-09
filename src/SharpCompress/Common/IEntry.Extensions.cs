using System.IO;

namespace SharpCompress.Common;

internal static class EntryExtensions
{
    internal static void PreserveExtractionOptions(this IEntry entry, string destinationFileName)
    {
        if (entry.Options.PreserveFileTime || entry.Options.PreserveAttributes)
        {
            var nf = new FileInfo(destinationFileName);
            if (!nf.Exists)
            {
                return;
            }

            // update file time to original packed time
            if (entry.Options.PreserveFileTime)
            {
                if (entry.CreatedTime.HasValue)
                {
                    nf.CreationTime = entry.CreatedTime.Value;
                }

                if (entry.LastModifiedTime.HasValue)
                {
                    nf.LastWriteTime = entry.LastModifiedTime.Value;
                }

                if (entry.LastAccessedTime.HasValue)
                {
                    nf.LastAccessTime = entry.LastAccessedTime.Value;
                }
            }

            if (entry.Options.PreserveAttributes)
            {
                if (entry.Attrib.HasValue)
                {
                    nf.Attributes = (FileAttributes)
                        System.Enum.ToObject(typeof(FileAttributes), entry.Attrib.Value);
                }
            }
        }
    }
}
