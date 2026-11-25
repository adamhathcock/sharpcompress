using System.Collections.Generic;
using System.Text.Json.Serialization;
using SharpCompress.Common;
using SharpCompress.Detection;

namespace SharpCompress.Cli;

internal sealed record ResultDocument<T>(T Result)
{
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; } = 1;
}

internal sealed record ErrorDocument(string Error, int ExitCode)
{
    [JsonPropertyOrder(-1)]
    public int SchemaVersion { get; } = 1;
}

internal sealed record DetectResult(
    string Path,
    long PhysicalSize,
    string Status,
    FileDetection? Detection
);

internal sealed record InspectResult(
    string Path,
    long PhysicalSize,
    FileDetection Detection,
    ArchiveInformation? Information,
    [property: JsonPropertyOrder(1)] string? Limitations
)
{
    public string Integrity { get; } = "notTested";
}

internal sealed record ListResult(List<EntryRow> Entries)
{
    public string Integrity { get; } = "notTested";
}

internal sealed record EntryRow(
    long Index,
    EntryInformation? Metadata = null,
    TarMetadata? Tar = null,
    ZipMetadata? Zip = null,
    string? Kind = null,
    string? Name = null,
    string? NameSource = null,
    CompressionType? Compression = null
);

internal sealed record TarMetadata(string? Mode, long? UserId, long? GroupId);

internal sealed record ZipMetadata(string Comment);

internal sealed record FormatRow(
    string Kind,
    string Name,
    string[]? Extensions = null,
    bool? Archive = null,
    bool? Reader = null,
    bool? Writer = null,
    bool? Read = null,
    bool? Write = null
);

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true
)]
[JsonSerializable(typeof(ResultDocument<DetectResult>))]
[JsonSerializable(typeof(ResultDocument<InspectResult>))]
[JsonSerializable(typeof(ResultDocument<ListResult>))]
[JsonSerializable(typeof(ResultDocument<EntryRow>))]
[JsonSerializable(typeof(ResultDocument<List<FormatRow>>))]
[JsonSerializable(typeof(ResultDocument<FormatRow>))]
[JsonSerializable(typeof(ErrorDocument))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
