using System;
using System.IO;

namespace SharpCompress.Compressors;

/// <summary>
/// Provides context information for compression operations.
/// Carries format-specific parameters that some compression types require.
/// </summary>
public sealed record CompressionContext
{
    /// <summary>
    /// The size of the input data, or -1 if unknown.
    /// </summary>
    public long InputSize { get; init; } = -1;

    /// <summary>
    /// The expected output size, or -1 if unknown.
    /// </summary>
    public long OutputSize { get; init; } = -1;

    /// <summary>
    /// Properties bytes for the compression format (e.g., LZMA properties).
    /// </summary>
    public byte[]? Properties { get; init; }

    /// <summary>
    /// Whether the underlying stream supports seeking.
    /// </summary>
    public bool CanSeek { get; init; }

    /// <summary>
    /// Additional format-specific options.
    /// </summary>
    public object? FormatOptions { get; init; }

    /// <summary>
    /// Creates a CompressionContext from a stream.
    /// </summary>
    /// <param name="stream">The stream to extract context from.</param>
    /// <returns>A CompressionContext populated from the stream.</returns>
    public static CompressionContext FromStream(Stream stream) =>
        new() { CanSeek = stream.CanSeek, InputSize = stream.CanSeek ? stream.Length : -1 };
}
