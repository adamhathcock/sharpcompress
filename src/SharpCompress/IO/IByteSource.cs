using System.IO;

namespace SharpCompress.IO;

/// <summary>
/// Represents a source of bytes that can be read as a stream.
/// This abstraction distinguishes between the "stream of bytes" concept
/// and the physical container (file, stream, or volume) that holds those bytes.
///
/// <para>
/// The key insight is that in archive formats, there are three distinct concepts:
/// </para>
///
/// <list type="number">
/// <item>
/// <description>
/// <b>ByteSource</b>: A logical source of bytes. This could be:
/// <list type="bullet">
/// <item>A single file or stream</item>
/// <item>A contiguous sequence spanning multiple files (split archives)</item>
/// <item>A compressed data block that expands to multiple files (SOLID archives)</item>
/// </list>
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Volume</b>: A physical container representing one archive file/stream.
/// In multi-volume archives (like RAR volumes), each volume is an independent
/// archive unit with its own headers and metadata.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>SourceStream</b>: Manages multiple byte sources and presents them as a
/// unified stream. Handles both split mode (contiguous bytes across files)
/// and volume mode (independent archive units).
/// </description>
/// </item>
/// </list>
///
/// <para>
/// Format-specific behaviors:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>7Zip</b>: Can have a contiguous stream of bytes for a file or files
/// within a folder (compression unit).
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>RAR SOLID</b>: Uses a contiguous stream of bytes for files, where
/// decompression depends on previous files in the stream.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>ZIP Split</b>: File data can be split across multiple disk files,
/// treated as one contiguous byte stream.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>RAR Multi-volume</b>: Each volume is an independent archive that
/// can be opened and read separately.
/// </description>
/// </item>
/// </list>
/// </summary>
public interface IByteSource
{
    /// <summary>
    /// Gets the index of this byte source within a collection of sources.
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Gets the length of bytes available from this source, if known.
    /// Returns null if the length cannot be determined (e.g., for unseekable streams).
    /// </summary>
    long? Length { get; }

    /// <summary>
    /// Gets the file name associated with this byte source, if available.
    /// </summary>
    string? FileName { get; }

    /// <summary>
    /// Opens a stream to read bytes from this source.
    /// </summary>
    /// <returns>A readable stream positioned at the start of the byte source.</returns>
    Stream OpenRead();

    /// <summary>
    /// Indicates whether this byte source represents part of a contiguous
    /// byte sequence that spans multiple sources (e.g., split archive).
    /// </summary>
    bool IsPartOfContiguousSequence { get; }
}
