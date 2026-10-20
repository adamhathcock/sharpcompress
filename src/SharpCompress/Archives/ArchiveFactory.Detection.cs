using System.IO;
using SharpCompress.Common;
using SharpCompress.Detection;
using SharpCompress.Factories;
using SharpCompress.Readers;

namespace SharpCompress.Archives;

public static partial class ArchiveFactory
{
    private static void DisposeProbeStream(Stream stream)
    {
        try
        {
#pragma warning disable VSTHRD103 // Probe streams may validate unread trailers during disposal.
            stream.Dispose();
#pragma warning restore VSTHRD103
        }
        catch
        {
            // Probes intentionally read only enough data to identify tar content.
        }
    }

    private static CompressionType? GetCompressedTarType(IFactory factory) =>
        factory switch
        {
            GZipFactory => CompressionType.GZip,
            LzwFactory => CompressionType.Lzw,
            _ => null,
        };

    private static ArchiveDetection CreateDetection(IFactory factory)
    {
        var supportedApis = ArchiveAccessMode.None;
        if (factory is IArchiveFactory)
        {
            supportedApis |= ArchiveAccessMode.Archive;
        }
        if (factory is IReaderFactory)
        {
            supportedApis |= ArchiveAccessMode.Reader;
        }

        return new ArchiveDetection(factory.KnownArchiveType, factory.Name, null, supportedApis);
    }

    private static ArchiveDetection CreateCompressedTarDetection(CompressionType compressionType) =>
        new(ArchiveType.Tar, "Tar", compressionType, ArchiveAccessMode.Reader);
}
