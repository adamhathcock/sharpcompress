namespace SharpCompress.Common.Zip
{
    using SharpCompress.Common;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    public class ZipVolume : Volume
    {
        [CompilerGenerated]
        private string _Comment_k__BackingField;

        public ZipVolume(Stream stream, Options options) : base(stream, options)
        {
        }

        public string Comment
        {
            [CompilerGenerated]
            get
            {
                return this._Comment_k__BackingField;
            }
            [CompilerGenerated]
            internal set
            {
                this._Comment_k__BackingField = value;
            }
        }
    }
}

