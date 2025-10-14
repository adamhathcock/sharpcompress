using System;
using System.IO;
using System.Linq;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.IO;

namespace SharpCompress.Readers;

public static class ReaderFactory
{
  /// <summary>
  /// Opens a Reader for Non-seeking usage
  /// </summary>
  /// <param name="stream"></param>
  /// <param name="options"></param>
  /// <returns></returns>
  public static IReader Open(Stream stream, ReaderOptions? options = null)
  {
    stream.CheckNotNull(nameof(stream));
    options ??= new ReaderOptions() { LeaveStreamOpen = false };

    var bStream = new SharpCompressStream(stream, bufferSize: options.BufferSize);

    long pos = bStream.GetPosition();

    Factory? testedFactory = null;

    if (!string.IsNullOrWhiteSpace(options.ExtensionHint))
    {
      testedFactory = Factory
        .Factories.OfType<Factories.Factory>()
        .FirstOrDefault(a =>
          a.GetSupportedExtensions()
            .Contains(options.ExtensionHint, StringComparer.CurrentCultureIgnoreCase)
        );
      if (testedFactory?.TryOpenReader(bStream, options, out var reader) == true && reader != null)
      {
        return reader;
      }
      bStream.StackSeek(pos);
    }

    foreach (var factory in Factory.Factories.OfType<Factories.Factory>())
    {
      if (testedFactory == factory)
      {
        continue; // Already tested above
      }
      bStream.StackSeek(pos);
      if (factory.TryOpenReader(bStream, options, out var reader) && reader != null)
      {
        return reader;
      }
    }

    throw new InvalidFormatException(
      "Cannot determine compressed stream type.  Supported Reader Formats: Arc, Zip, GZip, BZip2, Tar, Rar, LZip, XZ, ZStandard"
    );
  }
}
