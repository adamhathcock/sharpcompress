using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCompress.Common.Zip.Headers
{
    internal class LocalEntryHeader : ZipFileEntry
    {
        public LocalEntryHeader(ArchiveEncoding archiveEncoding)
            : base(ZipHeaderType.LocalEntry, archiveEncoding)
        {
        }

        internal override async ValueTask Read(Stream stream, CancellationToken cancellationToken)
        {
            Version = await stream.ReadUInt16(cancellationToken);
            Flags = (HeaderFlags)await stream.ReadUInt16(cancellationToken);
            CompressionMethod = (ZipCompressionMethod)await stream.ReadUInt16(cancellationToken);
            LastModifiedTime = await stream.ReadUInt16(cancellationToken);
            LastModifiedDate = await stream.ReadUInt16(cancellationToken);
            Crc = await stream.ReadUInt32(cancellationToken);
            CompressedSize = await stream.ReadUInt32(cancellationToken);
            UncompressedSize = await stream.ReadUInt32(cancellationToken);
            ushort nameLength = await stream.ReadUInt16(cancellationToken);
            ushort extraLength = await stream.ReadUInt16(cancellationToken);
            byte[] name = await stream.ReadBytes(nameLength, cancellationToken);
            byte[] extra = await stream.ReadBytes(extraLength, cancellationToken);

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
            }
        }

        internal ushort Version { get; private set; }
    }
}