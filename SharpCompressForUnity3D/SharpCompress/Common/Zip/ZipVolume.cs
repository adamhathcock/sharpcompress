namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    public class ZipVolume : Volume
    {
        [CompilerGenerated]
        private string <Comment>k__BackingField;

        public ZipVolume(Stream stream, Options options) : base(stream, options)
        {
        }

        public string Comment
        {
            [CompilerGenerated]
            get
            {
                return this.<Comment>k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this.<Comment>k__BackingField = value;
            }
        }
    }
}

