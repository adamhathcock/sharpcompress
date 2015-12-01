namespace SharpCompress.Reader.Rar
{
    using SharpCompress.Common;
    using SharpCompress.Common.Rar;
    using System;
    using System.IO;

    internal class SingleVolumeRarReader : RarReader
    {
        private readonly Stream stream;

        internal SingleVolumeRarReader(Stream stream, string password, Options options) : base(options)
        {
            base.Password = password;
            this.stream = stream;
        }

        internal override Stream RequestInitialStream()
        {
            return this.stream;
        }

        internal override void ValidateArchive(RarVolume archive)
        {
            if (archive.IsMultiVolume)
            {
                throw new MultiVolumeExtractionException("Streamed archive is a Multi-volume archive.  Use different RarReader method to extract.");
            }
        }
    }
}

