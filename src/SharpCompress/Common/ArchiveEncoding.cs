using System;
using System.Text;

namespace SharpCompress.Common;

public class ArchiveEncoding : IArchiveEncoding
{
    public Encoding Default { get; set; } = Encoding.Default;
    public Encoding Password { get; set; } = Encoding.Default;
    public Encoding UTF8 { get; set; } = Encoding.UTF8;
    public Encoding? Forced { get; set; }
    public Func<byte[], int, int, string>? CustomDecoder { get; set; }
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

    /// <summary>
    /// Set this encoding when you want to force it for all encoding operations.
    /// </summary>
    public Encoding? Forced { get; set; }

    /// <summary>
    /// Set this when you want to use a custom method for all decoding operations.
    /// </summary>
    /// <returns>string Func(bytes, index, length)</returns>
    public Func<byte[], int, int, string>? CustomDecoder { get; set; }
}

public static class ArchiveEncodingExtensions
{
    public static Encoding GetEncoding(this IArchiveEncoding encoding) =>
        encoding.Forced ?? encoding.Default;

    public static Func<byte[], int, int, string> GetDecoder(this IArchiveEncoding encoding) =>
        encoding.CustomDecoder
        ?? ((bytes, index, count) => encoding.GetEncoding().GetString(bytes, index, count));

    public static byte[] Encode(this IArchiveEncoding encoding, string str) =>
        encoding.Default.GetBytes(str);

    public static string Decode(this IArchiveEncoding encoding, byte[] bytes) =>
        encoding.Decode(bytes, 0, bytes.Length);

    public static string Decode(
        this IArchiveEncoding encoding,
        byte[] bytes,
        int start,
        int length
    ) => encoding.GetDecoder()(bytes, start, length);
}
