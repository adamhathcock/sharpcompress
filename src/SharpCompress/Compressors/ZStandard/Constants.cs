namespace SharpCompress.Compressors.ZStandard;

internal class Constants
{
    //NOTE: https://docs.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/gcallowverylargeobjects-element#remarks
    //NOTE: https://github.com/dotnet/runtime/blob/v5.0.0-rtm.20519.4/src/libraries/System.Private.CoreLib/src/System/Array.cs#L27
    public const ulong MaxByteArrayLength = 0x7FFFFFC7;
}
