using System.IO;
using SharpCompress.Common.Options;

namespace SharpCompress.Providers;

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
    /// <remarks>
    /// This value is consumed by provider implementations that need caller-supplied metadata
    /// that is not tied to ReaderOptions. For archive header encoding, use <see cref="ReaderOptions"/> instead.
    /// Examples of valid FormatOptions values include compression properties (e.g., LZMA properties),
    /// format flags, or algorithm-specific configuration.
    /// </remarks>
    public object? FormatOptions { get; init; }

    /// <summary>
    /// Creates a CompressionContext from a stream.
    /// </summary>
    /// <param name="stream">The stream to extract context from.</param>
    /// <returns>A CompressionContext populated from the stream.</returns>
    public static CompressionContext FromStream(Stream stream) =>
        new() { CanSeek = stream.CanSeek, InputSize = stream.CanSeek ? stream.Length : -1 };

    /// <summary>
    /// Reader options for accessing archive metadata such as header encoding.
    /// </summary>
    public IReaderOptions? ReaderOptions { get; init; }

    /// <summary>
    /// Returns a new <see cref="CompressionContext"/> with the specified reader options.
    /// </summary>
    /// <param name="readerOptions">The reader options to set.</param>
    /// <returns>A new <see cref="CompressionContext"/> instance.</returns>
    public CompressionContext WithReaderOptions(IReaderOptions? readerOptions) =>
        this with
        {
            ReaderOptions = readerOptions,
        };
}
