namespace SharpCompress.Compressors.Rar;

/// <summary>
/// Factory interface for creating IRarUnpack instances.
/// Each created instance is owned by the caller and should be disposed when done.
/// </summary>
internal interface IRarUnpackFactory
{
    /// <summary>
    /// Creates a new IRarUnpack instance.
    /// The caller is responsible for disposing the returned instance.
    /// </summary>
    IRarUnpack Create();
}

/// <summary>
/// Factory for creating UnpackV1 instances (RAR v3 and earlier).
/// </summary>
internal sealed class UnpackV1Factory : IRarUnpackFactory
{
    public static readonly UnpackV1Factory Instance = new();

    public IRarUnpack Create() => new UnpackV1.Unpack();
}

/// <summary>
/// Factory for creating UnpackV2017 instances (RAR v5+).
/// </summary>
internal sealed class UnpackV2017Factory : IRarUnpackFactory
{
    public static readonly UnpackV2017Factory Instance = new();

    public IRarUnpack Create() => new UnpackV2017.Unpack();
}
