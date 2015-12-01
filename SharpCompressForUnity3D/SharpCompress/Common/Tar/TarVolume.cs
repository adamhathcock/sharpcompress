namespace SharpCompress.Common.Tar
{
    using SharpCompress.Common;
    using System;
    using System.IO;

    public class TarVolume : Volume
    {
        public TarVolume(Stream stream, Options options) : base(stream, options)
        {
        }
    }
}

