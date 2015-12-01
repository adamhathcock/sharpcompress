namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.IO;

    internal class SplitHeader : ZipHeader
    {
        public SplitHeader() : base(ZipHeaderType.Split)
        {
        }

        internal override void Read(BinaryReader reader)
        {
            throw new NotImplementedException();
        }

        internal override void Write(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}

