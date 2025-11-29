namespace SharpCompress.Compressors.ZStandard;

internal class ZstandardConstants
{
    /// <summary>
    /// Magic number found at start of ZStandard frame: 0xFD 0x2F 0xB5 0x28
    /// </summary>
    public const uint MAGIC = 0xFD2FB528;

    // https://docs.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/gcallowverylargeobjects-element#remarks
    // https://github.com/dotnet/runtime/blob/v5.0.0-rtm.20519.4/src/libraries/System.Private.CoreLib/src/System/Array.cs#L27
    public const ulong MaxByteArrayLength = 0x7FFFFFC7;
}
