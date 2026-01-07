using System;
using System.Text;

namespace SharpCompress.Common;

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
