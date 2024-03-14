using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SharpCompress.Archives;

internal abstract class ArchiveVolumeFactory
{
    internal static FileInfo? GetFilePart(int index, FileInfo part1) //base the name on the first part
    {
        FileInfo? item = null;

        //split 001, 002 ...
        var m = Regex.Match(part1.Name, @"^(.*\.)([0-9]+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            item = new FileInfo(
                Path.Combine(
                    part1.DirectoryName!,
                    String.Concat(
                        m.Groups[1].Value,
                        (index + 1).ToString().PadLeft(m.Groups[2].Value.Length, '0')
                    )
                )
            );

        if (item != null && item.Exists)
            return item;
        return null;
    }
}
