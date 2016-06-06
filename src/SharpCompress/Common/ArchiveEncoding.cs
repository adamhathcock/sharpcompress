using System.Text;

namespace SharpCompress.Common
{
    public static class ArchiveEncoding
    {
        /// <summary>
        /// Default encoding to use when archive format doesn't specify one.
        /// </summary>
        public static Encoding Default { get; set; }

        /// <summary>
        /// Encoding used by encryption schemes which don't comply with RFC 2898.
        /// </summary>
        public static Encoding Password { get; set; }

        static ArchiveEncoding()
        {
            Default = Encoding.UTF8;
            Password = Encoding.UTF8;
        }
    }
}