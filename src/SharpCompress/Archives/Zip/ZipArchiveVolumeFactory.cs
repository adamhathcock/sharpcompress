using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SharpCompress.Archives.Zip;

internal static class ZipArchiveVolumeFactory
{
    internal static FileInfo? GetFilePart(int index, FileInfo part1) //base the name on the first part
    {
        FileInfo? item = null;

        //load files with zip/zipx first. Swapped to end once loaded in ZipArchive
        //new style .zip, z01.. | .zipx, zx01 - if the numbers go beyond 99 then they use 100 ...1000 etc
        var m = Regex.Match(part1.Name, @"^(.*\.)(zipx?|zx?[0-9]+)$", RegexOptions.IgnoreCase);
        if (m.Success)
            item = new FileInfo(
                Path.Combine(
                    part1.DirectoryName!,
                    String.Concat(
                        m.Groups[1].Value,
                        Regex.Replace(m.Groups[2].Value, @"[^xz]", ""),
                        index.ToString().PadLeft(2, '0')
                    )
                )
            );
        else //split - 001, 002 ...
            return ArchiveVolumeFactory.GetFilePart(index, part1);

        if (item != null && item.Exists)
            return item;

        return null; //no more items
    }
}
