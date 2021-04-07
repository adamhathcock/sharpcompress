using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal class DirectoryEntryHeader : ZipFileEntry
    {
        public DirectoryEntryHeader(ArchiveEncoding archiveEncoding)
            : base(ZipHeaderType.DirectoryEntry, archiveEncoding)
        {
        }

        internal override async ValueTask Read(Stream stream, CancellationToken cancellationToken)
        {
            Version = await stream.ReadUInt16(cancellationToken);
            VersionNeededToExtract = await stream.ReadUInt16(cancellationToken);
            Flags = (HeaderFlags)await stream.ReadUInt16(cancellationToken);
            CompressionMethod = (ZipCompressionMethod)await stream.ReadUInt16(cancellationToken);
            LastModifiedTime = await stream.ReadUInt16(cancellationToken);
            LastModifiedDate = await stream.ReadUInt16(cancellationToken);
            Crc = await stream.ReadUInt32(cancellationToken);
            CompressedSize = await stream.ReadUInt32(cancellationToken);
            UncompressedSize = await stream.ReadUInt32(cancellationToken);
            ushort nameLength = await stream.ReadUInt16(cancellationToken);
            ushort extraLength = await stream.ReadUInt16(cancellationToken);
            ushort commentLength = await stream.ReadUInt16(cancellationToken);
            DiskNumberStart = await stream.ReadUInt16(cancellationToken);
            InternalFileAttributes = await stream.ReadUInt16(cancellationToken);
            ExternalFileAttributes = await stream.ReadUInt32(cancellationToken);
            RelativeOffsetOfEntryHeader = await stream.ReadUInt32(cancellationToken);

            byte[] name = await stream.ReadBytes(nameLength, cancellationToken);
            byte[] extra = await stream.ReadBytes(extraLength, cancellationToken);
            byte[] comment = await stream.ReadBytes(commentLength, cancellationToken);

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
                Comment = ArchiveEncoding.DecodeUTF8(comment);
            }
            else
            {
                Name = ArchiveEncoding.Decode(name);
                Comment = ArchiveEncoding.Decode(comment);
            }

            LoadExtra(extra);

            var unicodePathExtra = Extra.FirstOrDefault(u => u.Type == ExtraDataType.UnicodePathExtraField);
            if (unicodePathExtra != null)
            {
                Name = ((ExtraUnicodePathExtraField)unicodePathExtra).UnicodeName;
            }

            var zip64ExtraData = Extra.OfType<Zip64ExtendedInformationExtraField>().FirstOrDefault();
            if (zip64ExtraData != null)
            {
                if (CompressedSize == uint.MaxValue)
                {
                    CompressedSize = zip64ExtraData.CompressedSize;
                }

                if (UncompressedSize == uint.MaxValue)
                {
                    UncompressedSize = zip64ExtraData.UncompressedSize;
                }

                if (RelativeOffsetOfEntryHeader == uint.MaxValue)
                {
                    RelativeOffsetOfEntryHeader = zip64ExtraData.RelativeOffsetOfEntryHeader;
                }
            }
        }

        internal ushort Version { get; private set; }

        public ushort VersionNeededToExtract { get; set; }

        public long RelativeOffsetOfEntryHeader { get; set; }

        public uint ExternalFileAttributes { get; set; }

        public ushort InternalFileAttributes { get; set; }

        public ushort DiskNumberStart { get; set; }

        public string? Comment { get; private set; }
    }
}