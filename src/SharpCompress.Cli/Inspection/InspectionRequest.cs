namespace SharpCompress.Cli.Inspection;

public sealed record InspectionRequest
{
    public AccessMode AccessMode { get; init; } = AccessMode.Forward;
    public string? Password { get; init; }
    public bool LookForHeader { get; init; }
    public string? ExtensionHint { get; init; }
    public int? RewindableBufferSize { get; init; }
    public bool IncludeDirectories { get; init; }
    public int? Limit { get; init; }
}
