#if !NO_FILE
using System.IO;

namespace SharpCompress.Common
{
    internal static class IEntryExtensions
    {
        internal static void PreserveExtractionOptions(this IEntry entry, string destinationFileName,
                                                        ExtractOptions options)
        {
            if (options.HasFlag(ExtractOptions.PreserveFileTime) || options.HasFlag(ExtractOptions.PreserveAttributes))
            {
                FileInfo nf = new FileInfo(destinationFileName);
                if (!nf.Exists)
                {
                    return;
                }

                // update file time to original packed time
                if (options.HasFlag(ExtractOptions.PreserveFileTime))
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

                if (options.HasFlag(ExtractOptions.PreserveAttributes))
                {
                    if (entry.Attrib.HasValue)
                    {
                        nf.Attributes = (FileAttributes)System.Enum.ToObject(typeof(FileAttributes), entry.Attrib.Value);
                    }
                }
            }
        }
    }
}
#endif
