using System;
using SharpCompress.Common;

namespace SharpCompress.Writers;

internal static class CompressionLevelValidation
{
    public static void Validate(CompressionType compressionType, int compressionLevel)
    {
        switch (compressionType)
        {
            case CompressionType.Deflate:
            case CompressionType.Deflate64:
            case CompressionType.GZip:
                EnsureRange(compressionLevel, 0, 9, compressionType);
                break;
            case CompressionType.ZStandard:
                EnsureRange(compressionLevel, 1, 22, compressionType);
                break;
            default:
                if (compressionLevel != 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(compressionLevel),
                        compressionLevel,
                        $"Compression type {compressionType} does not support configurable compression levels. Use 0."
                    );
                }
                break;
        }
    }

    private static void EnsureRange(
        int compressionLevel,
        int minInclusive,
        int maxInclusive,
        CompressionType compressionType
    )
    {
        if (compressionLevel < minInclusive || compressionLevel > maxInclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(compressionLevel),
                compressionLevel,
                $"Compression level for {compressionType} must be between {minInclusive} and {maxInclusive}."
            );
        }
    }
}
