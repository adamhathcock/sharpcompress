using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Common.Rar.Headers;
using SharpCompress.Common;
using SharpCompress.Common.Rar;

namespace SharpCompress.Archive.Rar
{
    internal static class RarArchiveVolumeFactory
    {
        internal static IEnumerable<RarVolume> GetParts(IEnumerable<Stream> streams, string password, Options options)
        {
            foreach (Stream s in streams)
            {
                if (!s.CanRead || !s.CanSeek)
                {
                    throw new ArgumentException("Stream is not readable and seekable");
                }
                StreamRarArchiveVolume part = new StreamRarArchiveVolume(s, password, options);
                yield return part;
            }
        }

#if !NO_FILE
        internal static IEnumerable<RarVolume> GetParts(FileInfo fileInfo, string password, Options options)
        {
            FileInfoRarArchiveVolume part = new FileInfoRarArchiveVolume(fileInfo, password, options);
            yield return part;

            if (!part.ArchiveHeader.ArchiveHeaderFlags.HasFlag(ArchiveFlags.VOLUME))
            {
                yield break; //if file isn't volume then there is no reason to look
            }
            ArchiveHeader ah = part.ArchiveHeader;
            fileInfo = GetNextFileInfo(ah, part.FileParts.FirstOrDefault() as FileInfoRarFilePart);
            //we use fileinfo because rar is dumb and looks at file names rather than archive info for another volume
            while (fileInfo != null && fileInfo.Exists)
            {
                part = new FileInfoRarArchiveVolume(fileInfo, password, options);

                fileInfo = GetNextFileInfo(ah, part.FileParts.FirstOrDefault() as FileInfoRarFilePart);
                yield return part;
            }
        }

        private static FileInfo GetNextFileInfo(ArchiveHeader ah, FileInfoRarFilePart currentFilePart)
        {
            if (currentFilePart == null)
            {
                return null;
            }
            bool oldNumbering = !ah.ArchiveHeaderFlags.HasFlag(ArchiveFlags.NEWNUMBERING)
                                || currentFilePart.MarkHeader.OldFormat;
            if (oldNumbering)
            {
                return FindNextFileWithOldNumbering(currentFilePart.FileInfo);
            }
            else
            {
                return FindNextFileWithNewNumbering(currentFilePart.FileInfo);
            }
        }

        private static FileInfo FindNextFileWithOldNumbering(FileInfo currentFileInfo)
        {
            // .rar, .r00, .r01, ...
            string extension = currentFileInfo.Extension;

            StringBuilder buffer = new StringBuilder(currentFileInfo.FullName.Length);
            buffer.Append(currentFileInfo.FullName.Substring(0,
                                                             currentFileInfo.FullName.Length - extension.Length));
            if (string.Compare(extension, ".rar", StringComparison.OrdinalIgnoreCase) == 0)
            {
                buffer.Append(".r00");
            }
            else
            {
                int num = 0;
                if (int.TryParse(extension.Substring(2, 2), out num))
                {
                    num++;
                    buffer.Append(".r");
                    if (num < 10)
                    {
                        buffer.Append('0');
                    }
                    buffer.Append(num);
                }
                else
                {
                    ThrowInvalidFileName(currentFileInfo);
                }
            }
            return new FileInfo(buffer.ToString());
        }

        private static FileInfo FindNextFileWithNewNumbering(FileInfo currentFileInfo)
        {
            // part1.rar, part2.rar, ...
            string extension = currentFileInfo.Extension;
            if (string.Compare(extension, ".rar", StringComparison.OrdinalIgnoreCase) != 0)
            {
                throw new ArgumentException("Invalid extension, expected 'rar': " + currentFileInfo.FullName);
            }
            int startIndex = currentFileInfo.FullName.LastIndexOf(".part");
            if (startIndex < 0)
            {
                ThrowInvalidFileName(currentFileInfo);
            }
            StringBuilder buffer = new StringBuilder(currentFileInfo.FullName.Length);
            buffer.Append(currentFileInfo.FullName, 0, startIndex);
            int num = 0;
            string numString = currentFileInfo.FullName.Substring(startIndex + 5,
                                                                  currentFileInfo.FullName.IndexOf('.', startIndex + 5) -
                                                                  startIndex - 5);
            buffer.Append(".part");
            if (int.TryParse(numString, out num))
            {
                num++;
                for (int i = 0; i < numString.Length - num.ToString().Length; i++)
                {
                    buffer.Append('0');
                }
                buffer.Append(num);
            }
            else
            {
                ThrowInvalidFileName(currentFileInfo);
            }
            buffer.Append(".rar");
            return new FileInfo(buffer.ToString());
        }

        private static void ThrowInvalidFileName(FileInfo fileInfo)
        {
            throw new ArgumentException("Filename invalid or next archive could not be found:"
                                        + fileInfo.FullName);
        }

#endif
    }
}