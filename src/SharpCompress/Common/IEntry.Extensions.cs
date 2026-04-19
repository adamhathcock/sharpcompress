using System.IO;

namespace SharpCompress.Common;

internal static class EntryExtensions
{
    internal static void PreserveExtractionOptions(
        this IEntry entry,
        string destinationFileName,
        ExtractionOptions options
    )
    {
        if (options.PreserveFileTime || options.PreserveAttributes)
        {
            var nf = new FileInfo(destinationFileName);
            if (!nf.Exists)
            {
                return;
            }

            // update file time to original packed time
            if (options.PreserveFileTime)
            {
                if (entry.CreatedTime.HasValue)
                {
                    try
                    {
                        nf.CreationTime = entry.CreatedTime.Value;
                    }
                    catch
                    {
                        // Invalid time or the OS rejected
                    }
                }

                if (entry.LastModifiedTime.HasValue)
                {
                    try
                    {
                        nf.LastWriteTime = entry.LastModifiedTime.Value;
                    }
                    catch
                    {
                        // Invalid time or the OS rejected
                    }
                }

                if (entry.LastAccessedTime.HasValue)
                {
                    try
                    {
                        nf.LastAccessTime = entry.LastAccessedTime.Value;
                    }
                    catch
                    {
                        // Invalid time or the OS rejected
                    }
                }
            }

            if (options.PreserveAttributes)
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
