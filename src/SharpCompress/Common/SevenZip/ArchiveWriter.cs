using System.IO;
using SharpCompress.Compressors.LZMA.Utilities;

namespace SharpCompress.Common.SevenZip;

/// <summary>
/// Top-level orchestrator for writing 7z archive headers.
/// Assembles the complete header from StreamsInfo and FilesInfo,
/// and supports writing either a raw header (kHeader) or an
/// encoded/compressed header (kEncodedHeader).
/// </summary>
internal static class ArchiveHeaderWriter
{
    /// <summary>
    /// Writes a raw (uncompressed) header containing MainStreamsInfo and FilesInfo.
    /// </summary>
    public static void WriteRawHeader(
        Stream stream,
        SevenZipStreamsInfoWriter? mainStreamsInfo,
        SevenZipFilesInfoWriter? filesInfo
    )
    {
        stream.WriteByte((byte)BlockType.Header);

        if (mainStreamsInfo != null)
        {
            stream.WriteByte((byte)BlockType.MainStreamsInfo);
            mainStreamsInfo.Write(stream);
        }

        if (filesInfo != null)
        {
            stream.WriteByte((byte)BlockType.FilesInfo);
            filesInfo.Write(stream);
        }

        stream.WriteByte((byte)BlockType.End);
    }

    /// <summary>
    /// Writes an encoded header - a StreamsInfo block that describes
    /// how to decompress the actual header data.
    /// </summary>
    public static void WriteEncodedHeader(
        Stream stream,
        SevenZipStreamsInfoWriter headerStreamsInfo
    )
    {
        stream.WriteByte((byte)BlockType.EncodedHeader);
        headerStreamsInfo.Write(stream);
    }
}
