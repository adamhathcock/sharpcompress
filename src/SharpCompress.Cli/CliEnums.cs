namespace SharpCompress.Cli;

public enum AccessMode
{
    Forward,
    Seekable,
}

public enum OutputFormat
{
    Table,
    Json,
}

public enum StreamingType
{
    Forward,
    Seekable,
    AutoFallbackSeekable,
    SeekableMultiVolume,
}

public enum InspectionErrorCode
{
    InvalidPath,
    FileNotFound,
    NotArchive,
    AccessModeNotSupported,
    InspectionFailed,
    Unexpected,
}

internal enum InspectionRenderMode
{
    Inspect,
    List,
}
