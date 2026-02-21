using System.Text.Json.Serialization;
using SharpCompress.Cli.Formats;
using SharpCompress.Cli.Inspection;

namespace SharpCompress.Cli.Output;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true
)]
[JsonSerializable(typeof(InspectionExecutionResult))]
[JsonSerializable(typeof(ArchiveInspectionResult))]
[JsonSerializable(typeof(ArchiveEntryResult))]
[JsonSerializable(typeof(InspectionError))]
[JsonSerializable(typeof(FormatsReport))]
[JsonSerializable(typeof(FormatSupportResult))]
internal sealed partial class CliJsonSerializerContext : JsonSerializerContext;
