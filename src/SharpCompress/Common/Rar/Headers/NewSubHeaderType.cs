using System;

namespace SharpCompress.Common.Rar.Headers
{
    internal sealed class NewSubHeaderType : IEquatable<NewSubHeaderType>
    {
        internal static readonly NewSubHeaderType SUBHEAD_TYPE_CMT = new('C', 'M', 'T');

        //internal static final NewSubHeaderType SUBHEAD_TYPE_ACL = new (new byte[]{'A','C','L'});

        //internal static final NewSubHeaderType SUBHEAD_TYPE_STREAM = new (new byte[]{'S','T','M'});

        //internal static final NewSubHeaderType SUBHEAD_TYPE_UOWNER = new (new byte[]{'U','O','W'});

        //internal static final NewSubHeaderType SUBHEAD_TYPE_AV = new (new byte[]{'A','V'});

        internal static readonly NewSubHeaderType SUBHEAD_TYPE_RR = new('R', 'R');

        //internal static final NewSubHeaderType SUBHEAD_TYPE_OS2EA = new (new byte[]{'E','A','2'});

        //internal static final NewSubHeaderType SUBHEAD_TYPE_BEOSEA = new (new byte[]{'E','A','B','E'});

        private readonly byte[] _bytes;

        private NewSubHeaderType(params char[] chars)
        {
            _bytes = new byte[chars.Length];
            for (int i = 0; i < chars.Length; ++i)
            {
                _bytes[i] = (byte)chars[i];
            }
        }

        internal bool Equals(byte[] bytes)
        {
            if (_bytes.Length != bytes.Length)
            {
                return false;
            }

            return _bytes.AsSpan().SequenceEqual(bytes);
        }

        public bool Equals(NewSubHeaderType? other)
        {
            return other is not null && Equals(other._bytes);
        }
    }
}