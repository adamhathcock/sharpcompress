using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace SharpCompress.Cli;

internal static class OutputRenderer
{
    private static readonly CliJsonContext PrettyJson = new(
        new JsonSerializerOptions(CliJsonContext.Default.Options) { WriteIndented = true }
    );

    internal static Task RenderAsync<T>(T value, TextWriter output, bool json, bool ndjson)
    {
        var context = json ? PrettyJson : CliJsonContext.Default;
        var typeInfo = (JsonTypeInfo<ResultDocument<T>>?)
            context.GetTypeInfo(typeof(ResultDocument<T>))
            ?? throw new InvalidOperationException($"Missing CLI JSON metadata for {typeof(T)}.");
        var document = new ResultDocument<T>(value);
        if (!json && !ndjson)
        {
            var element = JsonSerializer.SerializeToElement(document, typeInfo);
            return WriteTextAsync(element.GetProperty("result"), output, "");
        }

        return output.WriteLineAsync(JsonSerializer.Serialize(document, typeInfo));
    }

    internal static Task WriteErrorAsync(
        TextWriter output,
        bool structured,
        string message,
        int code
    ) =>
        output.WriteLineAsync(
            structured
                ? JsonSerializer.Serialize(
                    new ErrorDocument(message, code),
                    CliJsonContext.Default.ErrorDocument
                )
                : message
        );

    internal static Task WriteEntryNameAsync(TextWriter output, string? name)
    {
        name ??= "(unnamed)";
        StringBuilder? escaped = null;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsControl(c) || c is '\u2028' or '\u2029')
            {
                escaped ??= new StringBuilder(name.Length).Append(name, 0, i);
                escaped.Append(@"\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                escaped?.Append(c);
            }
        }
        return output.WriteLineAsync(escaped?.ToString() ?? name);
    }

    private static async Task WriteTextAsync(JsonElement value, TextWriter output, string prefix)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                await WriteTextAsync(property.Value, output, prefix + property.Name + ".")
                    .ConfigureAwait(false);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                await WriteTextAsync(item, output, prefix + index++ + ".").ConfigureAwait(false);
            }
        }
        else if (value.ValueKind != JsonValueKind.Null)
        {
            await output
                .WriteLineAsync(prefix.TrimEnd('.') + ": " + value.GetRawText())
                .ConfigureAwait(false);
        }
    }

    internal static async Task WriteRowsAsync<T>(IEnumerable<T> rows, TextWriter output)
    {
        foreach (var row in rows)
        {
            await RenderAsync(row, output, false, true).ConfigureAwait(false);
        }
    }
}
