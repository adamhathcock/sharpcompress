using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Factories;
using SharpCompress.Providers;
using SharpCompress.Readers;
using SharpCompress.Writers;

namespace SharpCompress.Cli;

internal static class FormatsCommand
{
    internal static async Task<int> ExecuteAsync(
        string? path,
        TextWriter output,
        bool json,
        bool ndjson
    )
    {
        var rows = Factory
            .Factories.OrderBy(f => f.Name, StringComparer.Ordinal)
            .Where(f => path is null || f.Name.Equals(path, StringComparison.OrdinalIgnoreCase))
            .Select(f => new FormatRow(
                "archive",
                f.Name,
                f.GetSupportedExtensions().ToArray(),
                f is IArchiveFactory,
                f is IReaderFactory,
                f is IWriterFactory
            ))
            .ToList();
        foreach (
            var wrapper in TarWrapper.Wrappers.Where(w => w.CompressionType != CompressionType.None)
        )
        {
            var provider = CompressionProviderRegistry.Default.GetProvider(wrapper.CompressionType);
            if (
                path is null
                || wrapper
                    .CompressionType.ToString()
                    .Equals(path, StringComparison.OrdinalIgnoreCase)
            )
            {
                rows.Add(
                    new FormatRow(
                        "compressionStream",
                        wrapper.CompressionType.ToString(),
                        Read: provider?.SupportsDecompression == true,
                        Write: provider?.SupportsCompression == true
                    )
                );
            }
        }
        if (ndjson)
        {
            await OutputRenderer.WriteRowsAsync(rows, output).ConfigureAwait(false);
        }
        else
        {
            await OutputRenderer.RenderAsync(rows, output, json, false).ConfigureAwait(false);
        }
        return rows.Count == 0 ? 1 : 0;
    }
}
