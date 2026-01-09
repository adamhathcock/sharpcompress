using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.Ace.Headers;
using SharpCompress.Common.Zip.Headers;
using SharpCompress.Crypto;

namespace SharpCompress.Common.Ace.Headers
{
    /// <summary>
    /// ACE main archive header
    /// </summary>
    public sealed class AceMainHeader : AceHeader
    {
        public byte ExtractVersion { get; set; }
        public byte CreatorVersion { get; set; }
        public HostOS HostOS { get; set; }
        public byte VolumeNumber { get; set; }
        public DateTime DateTime { get; set; }
        public string Advert { get; set; } = string.Empty;
        public List<byte> Comment { get; set; } = new();
        public byte AceVersion { get; private set; }

        public AceMainHeader(IArchiveEncoding archiveEncoding)
            : base(archiveEncoding, AceHeaderType.MAIN) { }

        /// <summary>
        /// Reads the main archive header from the stream.
        /// Returns header if this is a valid ACE archive.
        /// Supports both ACE 1.0 and ACE 2.0 formats.
        /// </summary>
        public override AceHeader? Read(Stream stream)
        {
            var headerData = ReadHeader(stream);
            if (headerData.Length == 0)
            {
                return null;
            }
            int offset = 0;

            // Header type should be 0 for main header
            if (headerData[offset++] != HeaderType)
            {
                return null;
            }

            // Header flags (2 bytes)
            HeaderFlags = BitConverter.ToUInt16(headerData, offset);
            offset += 2;

            // Skip signature "**ACE**" (7 bytes)
            if (!CheckMagicBytes(headerData, offset))
            {
                throw new InvalidDataException("Invalid ACE archive signature.");
            }
            offset += 7;

            // ACE version (1 byte) - 10 for ACE 1.0, 20 for ACE 2.0
            AceVersion = headerData[offset++];
            ExtractVersion = headerData[offset++];

            // Host OS (1 byte)
            if (offset < headerData.Length)
            {
                var hostOsByte = headerData[offset++];
                HostOS = hostOsByte <= 11 ? (HostOS)hostOsByte : HostOS.Unknown;
            }
            // Volume number (1 byte)
            VolumeNumber = headerData[offset++];

            // Creation date/time (4 bytes)
            var dosDateTime = BitConverter.ToUInt32(headerData, offset);
            DateTime = ConvertDosDateTime(dosDateTime);
            offset += 4;

            // Reserved fields (8 bytes)
            if (offset + 8 <= headerData.Length)
            {
                offset += 8;
            }

            // Skip additional fields based on flags
            // Handle comment if present
            if ((HeaderFlags & SharpCompress.Common.Ace.Headers.HeaderFlags.COMMENT) != 0)
            {
                if (offset + 2 <= headerData.Length)
                {
                    ushort commentLength = BitConverter.ToUInt16(headerData, offset);
                    offset += 2 + commentLength;
                }
            }

            return this;
        }
    }
}
