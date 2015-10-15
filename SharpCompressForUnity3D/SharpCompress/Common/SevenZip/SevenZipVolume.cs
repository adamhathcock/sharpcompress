namespace SharpCompress.Common.SevenZip
{
    using SharpCompress.Common;
    using System;
    using System.IO;

    public class SevenZipVolume : Volume
    {
        public SevenZipVolume(Stream stream, Options options) : base(stream, options)
        {
        }
    }
}

