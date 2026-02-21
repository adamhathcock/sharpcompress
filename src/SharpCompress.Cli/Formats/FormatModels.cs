using System.Collections.Generic;

namespace SharpCompress.Cli.Formats;

public sealed record FormatSupportResult(
    string Name,
    string ArchiveType,
    List<string> Extensions,
    bool SupportsForward,
    bool SupportsSeekable
);

public sealed record FormatsReport(List<FormatSupportResult> Formats);
