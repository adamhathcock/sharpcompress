// Added by drone1400, December 2025
// Location: https://github.com/drone1400/bzip2

namespace SharpCompress.Compressors.BZip2MT.Interface
{
    internal static class BZip2Constants
    {
        public const uint STREAM_START_MARKER = 0x425A68;
        public const ulong STREAM_END_MARKER = 0x177245385090;
        public const ulong BLOCK_HEADER_MARKER = 0x314159265359;

        /// <summary>The first 2 bytes of a Bzip2 marker, BZ</summary>
        public const uint STREAM_START_MARKER_1 = 0x425a;

        /// <summary>The 'h' that distinguishes BZip from BZip2</summary>
        public const uint STREAM_START_MARKER_2 = 0x68;

        /// <summary>First three bytes of the end of stream marker</summary>
        public const uint STREAM_END_MARKER_1 = 0x177245;

        /// <summary>Last three bytes of the end of stream marker</summary>
        public const uint STREAM_END_MARKER_2 = 0x385090;

        // First three bytes of the block header marker
        public const uint BLOCK_HEADER_MARKER_1 = 0x314159;

        // Last three bytes of the block header marker
        public const uint BLOCK_HEADER_MARKER_2 = 0x265359;
    }

    public enum InputStreamHeaderCheckType
    {
        // check for full header (ex: BZh9 )
        FULL_HEADER,

        // skips BZ part of header
        NO_BZ,

        // skips BZh part of header
        NO_BZH,

        // skips BZh and block level, aka the whole header
        NO_HEADER,
    }
}
