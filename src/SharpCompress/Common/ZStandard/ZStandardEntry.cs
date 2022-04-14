﻿using System;
using System.Collections.Generic;
using System.IO;
using SharpCompress.Common.GZip;

namespace SharpCompress.Common.ZStandard
{
    public class ZStandardEntry : Entry
    {
        private readonly ZStandardFilePart _filePart;

        internal ZStandardEntry(ZStandardFilePart filePart)
        {
            _filePart = filePart;
        }

        public override CompressionType CompressionType => CompressionType.GZip;

        public override long Crc => _filePart.Crc ?? 0;

        public override string Key => _filePart.FilePartName;

        public override string? LinkTarget => null;

        public override long CompressedSize => 0;

        public override long Size => _filePart.UncompressedSize ?? 0;

        public override DateTime? LastModifiedTime => _filePart.DateModified;

        public override DateTime? CreatedTime => null;

        public override DateTime? LastAccessedTime => null;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => false;

        public override bool IsDirectory => false;

        public override bool IsSplitAfter => false;

        internal override IEnumerable<FilePart> Parts => _filePart.AsEnumerable<FilePart>();

        internal static IEnumerable<GZipEntry> GetEntries(Stream stream, OptionsBase options)
        {
            yield return new GZipEntry(new GZipFilePart(stream, options.ArchiveEncoding));
        }
    }
}