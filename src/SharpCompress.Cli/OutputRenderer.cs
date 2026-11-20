using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SharpCompress.Cli;

internal static class OutputRenderer
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly JsonSerializerOptions PrettyJson = new(Json) { WriteIndented = true };

    internal static Task RenderAsync(object value, TextWriter output, bool json, bool ndjson)
    {
        if (!json && !ndjson)
        {
            return WriteTextAsync(JsonSerializer.SerializeToElement(value, Json), output, "");
        }
        if (ndjson && value is IEnumerable<object> rows)
        {
            return WriteRowsAsync(rows, output);
        }

        return output.WriteLineAsync(
            JsonSerializer.Serialize(
                new { SchemaVersion = 1, Result = value },
                ndjson ? Json : PrettyJson
            )
        );
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

    private static async Task WriteRowsAsync(IEnumerable<object> rows, TextWriter output)
    {
        foreach (var row in rows)
        {
            await RenderAsync(row, output, false, true).ConfigureAwait(false);
        }
    }
}
