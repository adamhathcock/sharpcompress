namespace SharpCompress.Common.Zip.Headers
{
    using System;
    using System.Text;

    internal class ExtraUnicodePathExtraField : ExtraData
    {
        internal byte[] NameCRC32
        {
            get
            {
                byte[] dst = new byte[4];
                Buffer.BlockCopy(base.DataBytes, 1, dst, 0, 4);
                return dst;
            }
        }

        internal string UnicodeName
        {
            get
            {
                int count = base.Length - 5;
                return Encoding.UTF8.GetString(base.DataBytes, 5, count);
            }
        }

        internal byte Version
        {
            get
            {
                return base.DataBytes[0];
            }
        }
    }
}

