using System.IO;
using System.Linq;

namespace SharpCompress.Common.Zip.Headers;

internal class LocalEntryHeader : ZipFileEntry
{
    public LocalEntryHeader(ArchiveEncoding archiveEncoding)
        : base(ZipHeaderType.LocalEntry, archiveEncoding) { }

    internal override void Read(BinaryReader reader)
    {
        Version = reader.ReadUInt16();
        Flags = (HeaderFlags)reader.ReadUInt16();
        CompressionMethod = (ZipCompressionMethod)reader.ReadUInt16();
        OriginalLastModifiedTime = LastModifiedTime = reader.ReadUInt16();
        OriginalLastModifiedDate = LastModifiedDate = reader.ReadUInt16();
        Crc = reader.ReadUInt32();
        CompressedSize = reader.ReadUInt32();
        UncompressedSize = reader.ReadUInt32();
        var nameLength = reader.ReadUInt16();
        var extraLength = reader.ReadUInt16();
        var name = reader.ReadBytes(nameLength);
        var extra = reader.ReadBytes(extraLength);

        // According to .ZIP File Format Specification
        //
        // For example: https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT
        //
        // Bit 11: Language encoding flag (EFS).  If this bit is set,
        //         the filename and comment fields for this file
        //         MUST be encoded using UTF-8. (see APPENDIX D)

        if (Flags.HasFlag(HeaderFlags.Efs))
        {
            Name = ArchiveEncoding.DecodeUTF8(name);
        }
        else
        {
            Name = ArchiveEncoding.Decode(name);
        }

        LoadExtra(extra);

        var unicodePathExtra = Extra.FirstOrDefault(u =>
            u.Type == ExtraDataType.UnicodePathExtraField
        );
        if (unicodePathExtra != null && ArchiveEncoding.Forced == null)
        {
            Name = ((ExtraUnicodePathExtraField)unicodePathExtra).UnicodeName;
        }

        var zip64ExtraData = Extra.OfType<Zip64ExtendedInformationExtraField>().FirstOrDefault();
        if (zip64ExtraData != null)
        {
            zip64ExtraData.Process(UncompressedSize, CompressedSize, 0, 0);

            if (CompressedSize == uint.MaxValue)
            {
                CompressedSize = zip64ExtraData.CompressedSize;
            }
            if (UncompressedSize == uint.MaxValue)
            {
                UncompressedSize = zip64ExtraData.UncompressedSize;
            }
        }

        var unixTimeExtra = Extra.FirstOrDefault(u => u.Type == ExtraDataType.UnixTimeExtraField);

        if (unixTimeExtra is not null)
        {
            // Tuple order is last modified time, last access time, and creation time.
            var unixTimeTuple = ((UnixTimeExtraField)unixTimeExtra).UnicodeTimes;

            if (unixTimeTuple.Item1.HasValue)
            {
                var dosTime = Utility.DateTimeToDosTime(unixTimeTuple.Item1.Value);

                LastModifiedDate = (ushort)(dosTime >> 16);
                LastModifiedTime = (ushort)(dosTime & 0x0FFFF);
            }
            else if (unixTimeTuple.Item2.HasValue)
            {
                var dosTime = Utility.DateTimeToDosTime(unixTimeTuple.Item2.Value);

                LastModifiedDate = (ushort)(dosTime >> 16);
                LastModifiedTime = (ushort)(dosTime & 0x0FFFF);
            }
            else if (unixTimeTuple.Item3.HasValue)
            {
                var dosTime = Utility.DateTimeToDosTime(unixTimeTuple.Item3.Value);

                LastModifiedDate = (ushort)(dosTime >> 16);
                LastModifiedTime = (ushort)(dosTime & 0x0FFFF);
            }
        }
    }

    internal ushort Version { get; private set; }
}
