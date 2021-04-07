﻿#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Common;
using SharpCompress.IO;

namespace SharpCompress.Archives.GZip
{
    internal sealed class GZipWritableArchiveEntry : GZipArchiveEntry, IWritableArchiveEntry
    {
        private readonly bool closeStream;
        private readonly Stream stream;

        internal GZipWritableArchiveEntry(GZipArchive archive, Stream stream,
                                          string path, long size, DateTime? lastModified, bool closeStream)
            : base(archive, null)
        {
            this.stream = stream;
            Key = path;
            Size = size;
            LastModifiedTime = lastModified;
            this.closeStream = closeStream;
        }

        public override long Crc => 0;

        public override string Key { get; }

        public override long CompressedSize => 0;

        public override long Size { get; }

        public override DateTime? LastModifiedTime { get; }

        public override DateTime? CreatedTime => null;

        public override DateTime? LastAccessedTime => null;

        public override DateTime? ArchivedTime => null;

        public override bool IsEncrypted => false;

        public override bool IsDirectory => false;

        public override bool IsSplitAfter => false;

        internal override IEnumerable<FilePart> Parts => throw new NotImplementedException();

        Stream IWritableArchiveEntry.Stream => stream;

        public override ValueTask<Stream> OpenEntryStreamAsync(CancellationToken cancellationToken = default)
        {
            //ensure new stream is at the start, this could be reset
            stream.Seek(0, SeekOrigin.Begin);
            return new(new NonDisposingStream(stream));
        }

        internal override async ValueTask CloseAsync()
        {
            if (closeStream)
            {
                await stream.DisposeAsync();
            }
        }
    }
}