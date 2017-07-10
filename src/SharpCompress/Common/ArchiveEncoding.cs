using System;
using System.Text;

namespace SharpCompress.Common
{
    public class ArchiveEncoding
    {
        /// <summary>
        /// Default encoding to use when archive format doesn't specify one.
        /// </summary>
        public Encoding Default { get; set; }

        /// <summary>
        /// ArchiveEncoding used by encryption schemes which don't comply with RFC 2898.
        /// </summary>
        public Encoding Password { get; set; }
        
        /// <summary>
        /// Set this encoding when you want to force it for all encoding/decoding operations.
        /// </summary>
        public Encoding Forced { get; set; }

        public ArchiveEncoding()
        {
            Default = Encoding.UTF8;
            Password = Encoding.UTF8;
        }

        public string Decode(byte[] bytes)
        {
            return Decode(bytes, 0, bytes.Length);
        }

        public string Decode(byte[] bytes, int start, int length)
        {
            return GetEncoding().GetString(bytes, start, length);
        }

        public byte[] Encode(string str)
        {
            return GetEncoding().GetBytes(str);
        }

        public Encoding GetEncoding()
        {
            return Forced ?? Default ?? Encoding.UTF8;
        }
    }
}