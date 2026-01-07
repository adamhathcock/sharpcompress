using System;
using System.Text;

namespace SharpCompress.Common;

public class ArchiveEncoding : IArchiveEncoding
{
    public ArchiveEncoding()
    {
        Default = Encoding.Default;
        Password = Encoding.Default;
        UTF8 = Encoding.UTF8;
    }

    /// <summary>
    /// Default encoding to use when archive format doesn't specify one.
    /// </summary>
    public Encoding Default { get; set; }

    /// <summary>
    /// ArchiveEncoding used by encryption schemes which don't comply with RFC 2898.
    /// </summary>
    public Encoding Password { get; set; }

    /// <summary>
    /// Default encoding to use when archive format specifies UTF-8 encoding.
    /// </summary>
    public Encoding UTF8 { get; set; }
}

public interface IArchiveEncoding
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
    /// Default encoding to use when archive format specifies UTF-8 encoding.
    /// </summary>
    public Encoding UTF8 { get; set; }
}

public static class ArchiveEncodingExtensions
{
    public static byte[] Encode(this IArchiveEncoding encoding, string str) => encoding.Default.GetBytes(str);
    public static string Decode(this IArchiveEncoding encoding, byte[] bytes) => encoding.Default.GetString(bytes);

    public static string Decode(this IArchiveEncoding encoding, byte[] bytes, int start, int length) =>
        encoding.Default.GetString(bytes, start, length);
}
