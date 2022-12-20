using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SharpCompress.Archives.Rar;

internal static class RarArchiveVolumeFactory
{
    internal static FileInfo? GetFilePart(int index, FileInfo part1) //base the name on the first part
    {
        FileInfo? item = null;

        //new style rar - ..part1 | /part01 | part001 ....
        Match m = Regex.Match(part1.Name, @"^(.*\.part)([0-9]+)(\.rar)$", RegexOptions.IgnoreCase);
        if (m.Success)
            item = new FileInfo(
                Path.Combine(
                    part1.DirectoryName!,
                    String.Concat(
                        m.Groups[1].Value,
                        (index + 1).ToString().PadLeft(m.Groups[2].Value.Length, '0'),
                        m.Groups[3].Value
                    )
                )
            );
        else
        {
            //old style - ...rar, .r00, .r01 ...
            m = Regex.Match(part1.Name, @"^(.*\.r)(ar|[0-9]+)$", RegexOptions.IgnoreCase);
            if (m.Success)
                item = new FileInfo(
                    Path.Combine(
                        part1.DirectoryName!,
                        String.Concat(
                            m.Groups[1].Value,
                            index == 0
                                ? "ar"
                                : (index - 1).ToString().PadLeft(m.Groups[2].Value.Length, '0')
                        )
                    )
                );
            else //split .001, .002 ....
                return ArchiveVolumeFactory.GetFilePart(index, part1);
        }

        if (item != null && item.Exists)
            return item;

        return null; //no more items
    }
}
