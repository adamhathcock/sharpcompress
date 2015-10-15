namespace SharpCompress.Common.Rar.Headers
{
    using System;

    internal class NewSubHeaderType : IEquatable<NewSubHeaderType>
    {
        private byte[] bytes;
        internal static readonly NewSubHeaderType SUBHEAD_TYPE_CMT = new NewSubHeaderType(new char[] { 'C', 'M', 'T' });
        internal static readonly NewSubHeaderType SUBHEAD_TYPE_RR = new NewSubHeaderType(new char[] { 'R', 'R' });

        private NewSubHeaderType(params char[] chars)
        {
            this.bytes = new byte[chars.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                this.bytes[i] = (byte) chars[i];
            }
        }

        internal bool Equals(byte[] bytes)
        {
            if (this.bytes.Length != bytes.Length)
            {
                return false;
            }
            for (int i = 0; i < bytes.Length; i++)
            {
                if (this.bytes[i] != bytes[i])
                {
                    return false;
                }
            }
            return true;
        }

        public bool Equals(NewSubHeaderType other)
        {
            return this.Equals(other.bytes);
        }
    }
}

